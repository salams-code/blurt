using System.Net;
using System.Net.Http;
using System.Text;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class OpenAiWhisperTests
{
    [Fact]
    public async Task Posts_the_wav_as_multipart_to_audio_transcriptions_and_returns_the_text()
    {
        var handler = new CapturingHandler(ResponseWithText("hallo welt"));
        var transcriber = new OpenAiWhisper(
            new HttpClient(handler),
            baseUrl: "https://api.openai.com/v1",
            apiKey: "sk-test-123");

        var text = await transcriber.TranscribeAsync(Wav());

        Assert.Equal("hallo welt", text);

        // Request shape: POST {base}/audio/transcriptions with Bearer auth.
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "https://api.openai.com/v1/audio/transcriptions",
            handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-123", handler.Request.Headers.Authorization.Parameter);

        // Multipart body carries the audio under "file" and the model name.
        Assert.StartsWith("multipart/form-data", handler.ContentType);
        Assert.Contains("name=file", handler.RequestBody!.Replace("\"", ""));
        Assert.Contains("whisper-1", handler.RequestBody);
    }

    [Fact]
    public async Task A_non_success_status_throws_so_the_pipeline_reports_transcription_failed()
    {
        // Same fail-soft contract as LocalWhisper: a 401 (missing key), 500 or
        // timeout throws; the DictationPipeline catches it and surfaces
        // TranscriptionFailed instead of crashing.
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var transcriber = new OpenAiWhisper(
            new HttpClient(handler), "https://api.openai.com/v1", apiKey: "");

        await Assert.ThrowsAsync<HttpRequestException>(() => transcriber.TranscribeAsync(Wav()));
    }

    [Fact]
    public async Task An_empty_key_sends_no_authorization_header()
    {
        // Never send "Authorization: Bearer " with an empty key — the server's
        // 401 message is clearer without a malformed header.
        var handler = new CapturingHandler(ResponseWithText("x"));
        var transcriber = new OpenAiWhisper(
            new HttpClient(handler), "https://api.openai.com/v1", apiKey: "");

        await transcriber.TranscribeAsync(Wav());

        Assert.Null(handler.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task With_online_selected_the_pipeline_transcribes_through_the_mock_endpoint()
    {
        // The issue's acceptance test, end-to-end over the seams the app wires:
        // Online mode resolves OpenAiWhisper, the pipeline runs the WAV through
        // the (mock) API and injects the returned transcript.
        var handler = new CapturingHandler(ResponseWithText("hallo aus der cloud"));
        var transcriber = await TranscriberResolver.ResolveAsync(
            TranscriptionMode.Online, zeroNetwork: false,
            local: () => throw new InvalidOperationException("local must not be touched"),
            online: () => new OpenAiWhisper(
                new HttpClient(handler), "https://api.openai.com/v1", "sk-test"));
        var injector = new CapturingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Wav());

        Assert.Equal(DictationOutcome.Injected, outcome);
        Assert.Equal("hallo aus der cloud", injector.InjectedText);
        Assert.EndsWith("/audio/transcriptions", handler.Request!.RequestUri!.AbsolutePath);
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

    private static HttpResponseMessage ResponseWithText(string text) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"text": "{{text}}"}""", Encoding.UTF8, "application/json"),
        };

    private static MemoryStream Wav() => new([1, 2, 3, 4]);

    // Captures the one request the transcriber sends, body included, and replies
    // with the canned response — same pattern as the refiner's tests.
    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(ct);
            return response;
        }
    }
}
