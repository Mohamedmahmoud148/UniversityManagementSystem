using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Provider-agnostic abstraction for Speech-to-Text transcription.
    /// Current implementation: OpenAI Whisper via FastAPI.
    /// Future: Azure Speech, Google STT, AssemblyAI, etc.
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>
        /// Transcribes audio from a storage URL.
        /// FastAPI downloads the file directly — no large byte transfer between services.
        /// </summary>
        Task<SpeechToTextResult?> TranscribeFromUrlAsync(
            string audioUrl,
            string fileName,
            string mimeType,
            CancellationToken ct = default);
    }

    public class SpeechToTextResult
    {
        public string Transcript { get; set; } = string.Empty;
        public int? DurationSeconds { get; set; }
        public string Provider { get; set; } = "whisper";
    }
}
