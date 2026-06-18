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
        /// Transcribes audio bytes and returns the full text transcript.
        /// </summary>
        /// <param name="audioBytes">Raw audio file bytes.</param>
        /// <param name="fileName">Original file name (used to detect format).</param>
        /// <param name="mimeType">MIME type of the audio file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Full transcript text, or null if transcription failed.</returns>
        Task<SpeechToTextResult?> TranscribeAsync(
            byte[] audioBytes,
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
