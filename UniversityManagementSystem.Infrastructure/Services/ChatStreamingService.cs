using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Streams AI responses token-by-token via Server-Sent Events.
    ///
    /// Flow:
    ///   1. Validate conversation ownership
    ///   2. Fetch history (before saving current message)
    ///   3. Build academic context
    ///   4. Save user message
    ///   5. Yield "typing" SSE event
    ///   6. POST to FastAPI /chat/stream, proxy SSE tokens as they arrive
    ///   7. Assemble full response text
    ///   8. Save assistant message
    ///   9. Yield "completed" SSE event
    /// </summary>
    public class ChatStreamingService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<ChatStreamingService> logger) : IChatStreamingService
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        public async IAsyncEnumerable<string> StreamAsync(
            Ulid userId,
            SendMessageDto dto,
            string role,
            string? profileId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // ── 0. Validate conversation ──────────────────────────────────────
            var conversation = await context.Conversations.FindAsync(new object[] { dto.ConversationId }, ct);
            if (conversation == null || conversation.UserId != userId)
            {
                yield return Sse("error", new { message = "Conversation not found or access denied." });
                yield break;
            }

            role = string.IsNullOrWhiteSpace(role) ? "student" : role.ToLower();

            // ── 1. History (before saving) ────────────────────────────────────
            var history = await context.ChatMessages
                .Where(m => m.ConversationId == dto.ConversationId)
                .OrderByDescending(m => m.CreatedAt).Take(10)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { role = m.Sender, content = m.Content })
                .ToListAsync(ct);

            // ── 2. Academic context ───────────────────────────────────────────
            var academicCtx = await BuildContextAsync(userId, role, profileId, ct);

            // ── 3. Save user message ──────────────────────────────────────────
            var userMsg = new ChatMessage
            {
                ConversationId = dto.ConversationId,
                Content        = dto.ActualMessage,
                Sender         = "user",
                CreatedAt      = DateTime.UtcNow
            };
            context.ChatMessages.Add(userMsg);
            await context.SaveChangesAsync(ct);

            // ── 4. Typing signal ──────────────────────────────────────────────
            yield return Sse("typing", null);

            // ── 5. Call FastAPI /chat/stream ──────────────────────────────────
            var payload = new AiChatRequestDto
            {
                UserId          = userId.ToString(),
                Role            = role,
                Message         = dto.ActualMessage,
                History         = history.Cast<object>().ToArray(),
                AcademicContext = academicCtx,
                ConversationId  = conversation.Id.ToString()
            };

            var fullText = new StringBuilder();
            string? intent = null, tool = null, model = null;
            bool streamFailed = false;

            HttpResponseMessage? resp = null;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
                {
                    Content = JsonContent.Create(payload, options: _json)
                };
                req.Headers.Add("Accept", "text/event-stream");

                resp = await httpClient.SendAsync(
                    req, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    yield return Sse("error", new { message = $"AI service error {(int)resp.StatusCode}." });
                    streamFailed = true;
                }
            }
            catch (OperationCanceledException)
            {
                yield return Sse("cancelled", new { message = "Generation cancelled." });
                yield break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ChatStreamingService: FastAPI connection failed");
                yield return Sse("error", new { message = "AI service unavailable." });
                streamFailed = true;
            }

            if (!streamFailed && resp != null)
            {
                try
                {
                    await using var responseStream = await resp.Content.ReadAsStreamAsync(ct);
                    using var reader = new StreamReader(responseStream, Encoding.UTF8);

                    while (!reader.EndOfStream && !ct.IsCancellationRequested)
                    {
                        string? line;
                        try { line = await reader.ReadLineAsync(ct); }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { logger.LogWarning(ex, "stream read error"); break; }

                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (!line.StartsWith("data: ")) continue;

                        var raw = line["data: ".Length..].Trim();
                        if (raw == "[DONE]") break;

                        JsonElement frame;
                        try { frame = JsonSerializer.Deserialize<JsonElement>(raw); }
                        catch { continue; }

                        var type = frame.TryGetProperty("type", out var tp) ? tp.GetString() : null;

                        switch (type)
                        {
                            case "token":
                            case "thinking":
                                var content = frame.TryGetProperty("content", out var cv) ? cv.GetString() : null;
                                if (!string.IsNullOrEmpty(content))
                                {
                                    fullText.Append(content);
                                    yield return Sse("token", new { content });
                                }
                                break;

                            case "meta":
                                intent = frame.TryGetProperty("intent", out var iv) ? iv.GetString() : null;
                                tool   = frame.TryGetProperty("tool",   out var tv) ? tv.GetString() : null;
                                model  = frame.TryGetProperty("model",  out var mv) ? mv.GetString() : null;
                                break;

                            case "error":
                                var errMsg = frame.TryGetProperty("message", out var em) ? em.GetString() : "AI error";
                                yield return Sse("error", new { message = errMsg });
                                break;

                            case "done":
                                goto exitLoop;
                        }
                    }
                    exitLoop:;
                }
                finally
                {
                    resp.Dispose();
                }
            }

            // ── 6. Save assistant message ─────────────────────────────────────
            var responseText = fullText.Length > 0
                ? fullText.ToString()
                : (streamFailed ? "" : "لم أتمكن من الإجابة. حاول مرة أخرى.");

            if (responseText.Length > 0)
            {
                var aiMsg = new ChatMessage
                {
                    ConversationId = dto.ConversationId,
                    Content        = responseText,
                    Sender         = "assistant",
                    Intent         = intent,
                    CreatedAt      = DateTime.UtcNow
                };
                context.ChatMessages.Add(aiMsg);

                // Auto-title on first exchange
                if (history.Count == 0 && conversation.Title == "New Chat")
                {
                    var words = dto.ActualMessage.Split(' ');
                    conversation.Title = string.Join(" ", words.Take(5));
                }

                try { await context.SaveChangesAsync(CancellationToken.None); }
                catch (Exception ex) { logger.LogError(ex, "ChatStreamingService: failed to save assistant message"); }

                // ── 7. Completed ──────────────────────────────────────────────
                yield return Sse("completed", new
                {
                    messageId      = aiMsg.Id.ToString(),
                    intent         = intent ?? "unknown",
                    tool           = tool ?? "none",
                    model          = model ?? "unknown",
                    conversationId = conversation.Id.ToString()
                });
            }
        }

        // ── SSE frame builder ─────────────────────────────────────────────────

        private static string Sse(string type, object? extra)
        {
            string json;
            if (extra == null)
            {
                json = $"{{\"type\":\"{type}\"}}";
            }
            else
            {
                var dict = new Dictionary<string, object?> { ["type"] = type };
                var extraJson = JsonSerializer.Serialize(extra, _json);
                var extraDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(extraJson);
                if (extraDict != null)
                    foreach (var kv in extraDict)
                        dict[kv.Key] = kv.Value;
                json = JsonSerializer.Serialize(dict, _json);
            }
            return $"data: {json}\n\n";
        }

        // ── Academic context (mirrors ChatService) ────────────────────────────

        private async Task<object> BuildContextAsync(
            Ulid userId, string role, string? profileId, CancellationToken ct)
        {
            try
            {
                if (role == "student" && !string.IsNullOrEmpty(profileId) && Ulid.TryParse(profileId, out var sid))
                {
                    var s = await context.Students.AsNoTracking()
                        .Include(x => x.Batch).Include(x => x.Department).Include(x => x.College)
                        .FirstOrDefaultAsync(x => x.Id == sid, ct);
                    if (s != null)
                        return new
                        {
                            userId       = userId.ToString(),
                            studentId    = sid.ToString(),
                            studentName  = s.FullName,
                            batchId      = s.BatchId.ToString(),
                            batchName    = s.Batch?.Name ?? "",
                            departmentId = s.DepartmentId.ToString(),
                            departmentName = s.Department?.Name ?? "",
                            collegeName  = s.College?.Name ?? "",
                            role
                        };
                }

                if (role == "doctor" && !string.IsNullOrEmpty(profileId) && Ulid.TryParse(profileId, out var did))
                {
                    var d = await context.Doctors.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == did, ct);
                    if (d != null)
                        return new { userId = userId.ToString(), doctorId = did.ToString(), doctorName = d.FullName, role };
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "ChatStreamingService: BuildContextAsync failed"); }

            return new { userId = userId.ToString(), role };
        }
    }
}
