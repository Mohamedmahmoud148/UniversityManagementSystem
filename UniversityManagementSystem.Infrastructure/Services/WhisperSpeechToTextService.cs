using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.DTOs.Lecture;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Speech-to-Text via FastAPI /api/lecture/transcribe-url endpoint.
    /// Sends the R2 storage URL instead of raw bytes — FastAPI downloads
    /// and sends to Whisper directly, avoiding large file transfers between services.
    /// </summary>
    public class WhisperSpeechToTextService(
        HttpClient httpClient,
        ILogger<WhisperSpeechToTextService> logger) : ISpeechToTextService
    {
        public async Task<SpeechToTextResult?> TranscribeFromUrlAsync(
            string audioUrl,
            string fileName,
            string mimeType,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("WhisperSTT: transcribing via URL for {File}", fileName);

                var payload = new { audio_url = audioUrl, filename = fileName, mime_type = mimeType };

                using var response = await httpClient.PostAsJsonAsync(
                    "/api/lecture/transcribe-url", payload, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content
                    .ReadFromJsonAsync<FastApiTranscribeResponse>(cancellationToken: ct);

                if (result == null || string.IsNullOrWhiteSpace(result.Transcript))
                {
                    logger.LogWarning("WhisperSTT: empty transcript for {File}", fileName);
                    return null;
                }

                logger.LogInformation("WhisperSTT: got {Chars} chars for {File}", result.Transcript.Length, fileName);
                return new SpeechToTextResult
                {
                    Transcript      = result.Transcript,
                    DurationSeconds = result.DurationSeconds,
                    Provider        = result.Provider
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WhisperSTT: failed for {File}", fileName);
                return null;
            }
        }
    }
}
