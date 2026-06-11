using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Blurt.Core;

/// <summary>
/// Text → cleaned-up text. The seam every refined dictation mode runs through:
/// the raw transcript plus a system prompt go in, the rewritten text comes out.
/// Only text crosses this boundary — never audio. Faked in tests; the real
/// implementation is <see cref="OpenAiCompatibleRefiner"/>.
/// </summary>
public interface IRefiner
{
    /// <summary>
    /// Rewrites <paramref name="text"/> under <paramref name="systemPrompt"/>
    /// (e.g. <see cref="RefinementPrompts.Fix"/>) and returns the refined text.
    /// Throws on a transport/endpoint failure so the pipeline can fall back to
    /// the raw transcript.
    /// </summary>
    Task<string> RefineAsync(string text, string systemPrompt, CancellationToken ct = default);
}

/// <summary>
/// An <see cref="IRefiner"/> backed by any OpenAI-compatible Chat Completions
/// endpoint. The same client serves OpenAI cloud and a remote Ollama instance
/// with no code change — only the base URL, model and key differ (read from the
/// <see cref="BlurtConfig"/> and <see cref="SettingsStore"/>). The
/// <see cref="HttpClient"/> is injected so tests can drive a fake handler and
/// never touch the network.
/// </summary>
public sealed class OpenAiCompatibleRefiner : IRefiner
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenAiCompatibleRefiner(HttpClient http, string baseUrl, string model, string apiKey)
    {
        _http = http;
        // Normalise so exactly one slash joins the base URL and the path,
        // whether or not the configured base URL ends in "/".
        _endpoint = new Uri($"{baseUrl.TrimEnd('/')}/chat/completions");
        _model = model;
        _apiKey = apiKey;
    }

    public async Task<string> RefineAsync(string text, string systemPrompt, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(new ChatCompletionRequest(
                _model,
                [
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", text),
                ])),
        };

        // A local Ollama needs no key; only attach Bearer auth when one is set,
        // so an empty key does not send an "Authorization: Bearer " header.
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(ct);
        var content = body?.Choices is { Count: > 0 } choices
            ? choices[0].Message?.Content
            : null;

        return content ?? string.Empty;
    }

    // OpenAI Chat Completions wire shapes — only the fields Blurt sends/reads.
    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices);

    private sealed record Choice(
        [property: JsonPropertyName("message")] ResponseMessage? Message);

    private sealed record ResponseMessage(
        [property: JsonPropertyName("content")] string? Content);
}
