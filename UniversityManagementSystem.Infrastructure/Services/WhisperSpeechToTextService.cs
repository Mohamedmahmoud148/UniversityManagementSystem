using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.DTOs.Lecture;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Speech-to-Text via FastAPI /api/lecture/transcribe endpoint.
    /// FastAPI calls OpenAI Whisper API internally.
    /// This keeps the .NET side provider-agnostic — swap the FastAPI implementation
    /// to switch to Azure Speech, AssemblyAI, or any other provider.
    /// </summary>
    public class WhisperSpeechToTextService(
        HttpClient httpClient,
        ILogger<WhisperSpeechToTextService> logger) : ISpeechToTextService
    {
        public async Task<SpeechToTextResult?> TranscribeAsync(
            byte[] audioBytes, string fileName, string mimeType,
            CancellationToken ct = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var audioContent = new ByteArrayContent(audioBytes);
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                content.Add(audioContent, "file", fileName);

                logger.LogInformation("WhisperSTT: sending {Bytes} bytes for {File}", audioBytes.Length, fileName);

                using var response = await httpClient.PostAsync("/api/lecture/transcribe", content, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<FastApiTranscribeResponse>(cancellationToken: ct);
                if (result == null || string.IsNullOrWhiteSpace(result.Transcript))
                {
                    logger.LogWarning("WhisperSTT: empty transcript returned for {File}", fileName);
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
                logger.LogError(ex, "WhisperSTT: transcription failed for {File}", fileName);
                return null;
            }
        }
    }
}
