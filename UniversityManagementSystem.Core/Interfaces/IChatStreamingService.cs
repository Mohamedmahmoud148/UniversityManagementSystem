using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Provider-agnostic streaming chat abstraction.
    /// Implementation proxies SSE tokens from the FastAPI AI service.
    /// </summary>
    public interface IChatStreamingService
    {
        /// <summary>
        /// Saves the user message, streams AI tokens, saves the assistant message.
        /// Yields SSE-formatted lines ("data: {...}\n\n") for the controller to write.
        /// </summary>
        IAsyncEnumerable<string> StreamAsync(
            Ulid userId,
            SendMessageDto dto,
            string role,
            string? profileId,
            CancellationToken ct = default);
    }
}
