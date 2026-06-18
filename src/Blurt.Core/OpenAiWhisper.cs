using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blurt.Core;

/// <summary>
/// An <see cref="ITranscriber"/> backed by the OpenAI Whisper API (issue 12):
/// the recorded WAV is POSTed to <c>/audio/transcriptions</c> and the returned
/// text is the raw transcript. The online alternative to local whisper.cpp for
/// when local latency is unacceptable — it trades away the offline guarantee,
/// so verbatim Pur never uses it (see <see cref="TranscriberResolver"/>).
/// The <see cref="HttpClient"/> is injected so tests drive a fake handler.
/// </summary>
public sealed class OpenAiWhisper : ITranscriber
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiWhisper(HttpClient http, string baseUrl, string apiKey, string model = "whisper-1")
    {
        _http = http;
        // Normalise so exactly one slash joins the base URL and the path,
        // whether or not the configured base URL ends in "/".
        _endpoint = new Uri($"{baseUrl.TrimEnd('/')}/audio/transcriptions");
        _apiKey = apiKey;
        _model = model;
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
    {
        var file = new StreamContent(wavAudio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        var content = new MultipartFormDataContent
        {
            { file, "file", "dictation.wav" },
            { new StringContent(_model), "model" },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        // Throws on transport/HTTP failure — the pipeline treats that as
        // TranscriptionFailed (fail-soft), same contract as LocalWhisper.
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // F14: read through a hard byte cap rather than buffering an unbounded body.
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var json = await HttpResponseLimit.ReadAsStringAsync(stream, HttpResponseLimit.DefaultMaxBytes, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        var body = JsonSerializer.Deserialize<TranscriptionResponse>(json, WireJson);
        return body?.Text ?? string.Empty;
    }

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    // The Whisper transcription wire shape — only the field Blurt reads.
    private sealed record TranscriptionResponse(
        [property: JsonPropertyName("text")] string? Text);
}
