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
            bool cancelled = false;
            string? earlyError = null;

            // ── Connect to FastAPI (no yield inside try-catch) ────────────────
            HttpResponseMessage? resp = null;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
                {
                    Content = JsonContent.Create(payload, options: _json)
                };
                req.Headers.Add("Accept", "text/event-stream");
                resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    earlyError = $"AI service error {(int)resp.StatusCode}.";
                    streamFailed = true;
                }
            }
            catch (OperationCanceledException) { cancelled = true; }
            catch (Exception ex)
            {
                logger.LogError(ex, "ChatStreamingService: FastAPI connection failed");
                earlyError = "AI service unavailable.";
                streamFailed = true;
            }

            // Yield connection-level events outside try-catch
            if (cancelled) { yield return Sse("cancelled", new { message = "Generation cancelled." }); yield break; }
            if (earlyError != null) { yield return Sse("error", new { message = earlyError }); }

            // ── Read stream — collect pending events, yield OUTSIDE try-catch ──
            if (!streamFailed && resp != null)
            {
                var pending = new System.Collections.Generic.Queue<string>();

                var meta = await ReadStreamIntoQueueAsync(resp, pending, fullText, ct);
                intent = meta.Intent;
                tool   = meta.Tool;
                model  = meta.Model;

                resp.Dispose();

                // Yield all collected SSE frames now (safe — outside try-catch)
                while (pending.Count > 0)
                    yield return pending.Dequeue();
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

        // ── Stream reader helper — returns meta, no ref params (async-safe) ────

        private sealed record StreamMeta(string? Intent, string? Tool, string? Model);

        private async Task<StreamMeta> ReadStreamIntoQueueAsync(
            HttpResponseMessage resp,
            System.Collections.Generic.Queue<string> queue,
            StringBuilder fullText,
            CancellationToken ct)
        {
            string? intent = null, tool = null, model = null;
            try
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    string? line;
                    try { line = await reader.ReadLineAsync(ct); }
                    catch { break; }

                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

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
                                queue.Enqueue(Sse("token", new { content }));
                            }
                            break;

                        case "meta":
                            intent = frame.TryGetProperty("intent", out var iv) ? iv.GetString() : intent;
                            tool   = frame.TryGetProperty("tool",   out var tv) ? tv.GetString() : tool;
                            model  = frame.TryGetProperty("model",  out var mv) ? mv.GetString() : model;
                            break;

                        case "error":
                            var errMsg = frame.TryGetProperty("message", out var em) ? em.GetString() : "AI error";
                            queue.Enqueue(Sse("error", new { message = errMsg }));
                            return new StreamMeta(intent, tool, model);

                        case "done":
                            return new StreamMeta(intent, tool, model);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ChatStreamingService: stream reading failed");
            }
            return new StreamMeta(intent, tool, model);
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
