using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class OpenAiCompatibleRefinerTests
{
    [Fact]
    public async Task Posts_a_chat_completions_request_and_returns_the_refined_content()
    {
        var handler = new CapturingHandler(ResponseWithContent("Hallo Welt."));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1",
            model: "gpt-4o-mini",
            apiKey: "sk-test-123");

        var refined = await refiner.RefineAsync("ähm hallo welt", RefinementPrompts.Fix);

        // The refined content from the assistant message is returned verbatim.
        Assert.Equal("Hallo Welt.", refined);

        // Request went to the chat-completions endpoint under the base URL.
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "https://api.openai.com/v1/chat/completions",
            handler.Request.RequestUri!.ToString());

        // Bearer auth carries the key from the caller.
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-123", handler.Request.Headers.Authorization.Parameter);

        // Body carries the configured model, the system (Fix) prompt and the
        // transcript as the user message — and nothing audio-shaped.
        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var root = doc.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(RefinementPrompts.Fix, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("ähm hallo welt", messages[1].GetProperty("content").GetString());

        // Only text is ever sent — never audio.
        Assert.DoesNotContain("audio", handler.RequestBody, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_english_mode_sends_the_translation_prompt_and_returns_the_english_text()
    {
        // English mode (issue 10): the same refiner runs the German transcript
        // through the translation prompt and returns clean English. The only
        // difference from Fix is which system prompt is sent.
        var handler = new CapturingHandler(ResponseWithContent("Hello world, how are you?"));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1",
            model: "gpt-4o-mini",
            apiKey: "sk-test-123");

        var refined = await refiner.RefineAsync("hallo welt wie geht es dir", RefinementPrompts.English);

        // The English translation from the assistant message is returned verbatim.
        Assert.Equal("Hello world, how are you?", refined);

        // The system message carries the English (translation) prompt, and the
        // German transcript is the user message.
        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(RefinementPrompts.English, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("hallo welt wie geht es dir", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Uses_a_custom_base_url_and_model_unchanged_for_remote_ollama()
    {
        // The same client serves a remote OpenAI-compatible (e.g. Ollama) endpoint
        // with only base URL + model changed — no code change.
        var handler = new CapturingHandler(ResponseWithContent("bereinigt"));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "http://ollama.local:11434/v1",
            model: "llama3.1",
            apiKey: "");

        var refined = await refiner.RefineAsync("roh", RefinementPrompts.Fix);

        Assert.Equal("bereinigt", refined);
        Assert.Equal(
            "http://ollama.local:11434/v1/chat/completions",
            handler.Request!.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("llama3.1", doc.RootElement.GetProperty("model").GetString());
        // An empty key means no Authorization header (local Ollama needs none).
        Assert.Null(handler.Request.Headers.Authorization);
    }

    [Fact]
    public async Task A_trailing_slash_on_the_base_url_does_not_double_up_the_path()
    {
        var handler = new CapturingHandler(ResponseWithContent("x"));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1/",
            model: "gpt-4o-mini",
            apiKey: "sk");

        await refiner.RefineAsync("hi", RefinementPrompts.Fix);

        Assert.Equal(
            "https://api.openai.com/v1/chat/completions",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task A_non_success_status_throws_so_the_pipeline_can_fall_back()
    {
        // A 500/timeout/etc. is treated like an unreachable endpoint: the refiner
        // throws and the pipeline falls back to the raw transcript.
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1",
            model: "gpt-4o-mini",
            apiKey: "sk");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => refiner.RefineAsync("hi", RefinementPrompts.Fix));
    }

    [Fact]
    public async Task The_fix_pipeline_injects_the_refined_text_returned_by_the_mock_server()
    {
        // End-to-end over the Fix seam: real refiner + mock OpenAI-compatible
        // server, driven through the DictationPipeline the app uses.
        var handler = new CapturingHandler(ResponseWithContent("Hallo Welt, wie geht es dir?"));
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1",
            model: "gpt-4o-mini",
            apiKey: "sk");
        var transcriber = new FixedTranscriber("ähm hallo welt also wie geht's dir");
        var injector = new CapturingInjector();
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (text, ct) => refiner.RefineAsync(text, RefinementPrompts.Fix, ct));

        var outcome = await pipeline.RunAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        Assert.Equal(DictationOutcome.Injected, outcome);
        Assert.Equal("Hallo Welt, wie geht es dir?", injector.InjectedText);
    }

    [Fact]
    public async Task An_unreachable_endpoint_makes_the_fix_pipeline_inject_the_raw_text_offline()
    {
        // The mock server refuses the connection; the Fix pipeline must inject the
        // raw transcript and report RefinedOffline so the app shows the notice.
        var refiner = new OpenAiCompatibleRefiner(
            new HttpClient(new RefusingHandler()),
            baseUrl: "https://api.openai.com/v1",
            model: "gpt-4o-mini",
            apiKey: "sk");
        var transcriber = new FixedTranscriber("ähm hallo welt");
        var injector = new CapturingInjector();
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (text, ct) => refiner.RefineAsync(text, RefinementPrompts.Fix, ct));

        var outcome = await pipeline.RunAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        Assert.Equal(DictationOutcome.RefinedOffline, outcome);
        Assert.Equal("ähm hallo welt", injector.InjectedText);
    }

    // --- helpers ---

    private static HttpResponseMessage ResponseWithContent(string content)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-1",
            choices = new[]
            {
                new { index = 0, message = new { role = "assistant", content } },
            },
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FixedTranscriber : ITranscriber
    {
        private readonly string _text;
        public FixedTranscriber(string text) => _text = text;

        public Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
            => Task.FromResult(_text);
    }

    private sealed class CapturingInjector : ITextInjector
    {
        public string? InjectedText { get; private set; }

        public Task<bool> InjectAsync(string text, CancellationToken ct = default)
        {
            InjectedText = text;
            return Task.FromResult(true);
        }
    }

    /// <summary>Simulates an unreachable endpoint by refusing every request.</summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }

    /// <summary>
    /// Stands in for the network: captures the outgoing request (so the test can
    /// assert URL, auth and body) and returns a canned chat-completions response.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHandler(HttpResponseMessage response) => _response = response;

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
