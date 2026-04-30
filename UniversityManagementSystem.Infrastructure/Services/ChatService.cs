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
    /// Manages the AI chat pipeline.
    ///
    /// Responsibilities (ONLY these):
    ///   1. Validate conversation ownership.
    ///   2. Fetch conversation history from DB (before saving current message).
    ///   3. Enrich academic context with real user data from DB.
    ///   4. Save the user message to DB.
    ///   5. Call the FastAPI AI service via IAiService.
    ///   6. Save the AI response to DB.
    ///   7. Return the AI response DTO.
    ///
    /// NOT responsible for:
    ///   - Tool execution (FastAPI handles this internally)
    ///   - LLM calls
    ///   - Intent detection
    ///   - Second AI calls
    ///
    /// FastAPI already executes tools and returns the final natural-language
    /// response. ChatService must never intercept or re-execute that work.
    /// </summary>
    public class ChatService(
        AppDbContext context,
        IAiService aiService,
        ILogger<ChatService> logger) : IChatService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly ILogger<ChatService> _logger = logger;

        // ── Create Conversation ──────────────────────────────────────────────

        public async Task<Ulid> CreateConversationAsync(Ulid userId, string title)
        {
            var conversation = new Conversation
            {
                UserId   = userId,
                Title    = title,
                IsActive = true
            };
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation.Id;
        }

        // ── Fetch Messages ───────────────────────────────────────────────────

        public async Task<PaginatedChatResponseDto> GetConversationMessagesAsync(Ulid conversationId, int page = 1, int pageSize = 50)
        {
            var query = _context.ChatMessages
                .Where(m => m.ConversationId == conversationId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.CreatedAt) // Return chronologically (oldest to newest)
                .Select(m => new ChatResponseDto
                {
                    Id            = m.Id,
                    Content       = m.Content,
                    Sender        = m.Sender,
                    IsFallback    = m.IsFallback,
                    SentAt        = m.CreatedAt
                })
                .ToListAsync();

            return new PaginatedChatResponseDto
            {
                TotalCount = totalCount,
                PageNumber = page,
                PageSize   = pageSize,
                Items      = items
            };
        }

        // ── Fetch Conversations ──────────────────────────────────────────────

        public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(Ulid userId)
        {
            return await _context.Conversations
               .Where(c => c.UserId == userId && c.IsActive)
               .Select(c => new ConversationDto
               {
                   Id            = c.Id,
                   Title         = c.Title,
                   LastMessageAt = c.Messages
                       .OrderByDescending(m => m.CreatedAt)
                       .Select(m => m.CreatedAt)
                       .FirstOrDefault()
               })
               .ToListAsync();
        }

        // ── Send Message ─────────────────────────────────────────────────────

        public async Task<ChatResponseDto> SendMessageAsync(
            Ulid userId,
            SendMessageDto messageDto,
            string role,
            string? profileId = null)
        {
            // ── 0. Validate Conversation ──────────────────────────────────────
            var conversation = await _context.Conversations.FindAsync(messageDto.ConversationId);
            if (conversation == null || conversation.UserId != userId)
                throw new KeyNotFoundException("Conversation not found or access denied.");

            role = string.IsNullOrWhiteSpace(role) ? "student" : role.ToLower();

            // ── 1. Fetch history BEFORE saving the current message ────────────
            //    Fetching after would pollute history with the message we're
            //    about to process — the AI would see the same question twice.
            var history = await _context.ChatMessages
                .Where(m => m.ConversationId == messageDto.ConversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .OrderBy(m => m.CreatedAt)          // Restore chronological order
                .Select(m => new
                {
                    role    = m.Sender,
                    content = m.Content
                })
                .ToListAsync();

            // ── 2. Enrich academic context ────────────────────────────────────
            //    FastAPI's PlannerAgent uses academic_context to auto-fill
            //    tool parameters (userId, studentId, subjectOfferingId, etc.)
            //    without asking the user for data they already have implicitly.
            var academicContext = await BuildAcademicContextAsync(userId, role, profileId);

            // ── 3. Save User Message ──────────────────────────────────────────
            //    Saved AFTER fetching history to keep history clean.
            var userMsg = new ChatMessage
            {
                ConversationId = messageDto.ConversationId,
                Content        = messageDto.ActualMessage,
                Sender         = "user",
                IsFallback     = false,
                CreatedAt      = DateTime.UtcNow
            };
            _context.ChatMessages.Add(userMsg);
            await _context.SaveChangesAsync();

            // ── 4. Call AI Service ────────────────────────────────────────────
            //    FastAPI receives full context and returns a final natural-language
            //    response. All tool execution (exam creation, result queries, etc.)
            //    happens inside FastAPI. We do NOT inspect tool_used.
            var aiRequest = new AiChatRequestDto
            {
                UserId          = userId.ToString(),
                Role            = role,
                Message         = messageDto.ActualMessage,
                History         = history.Cast<object>().ToArray(),
                AcademicContext = academicContext,
                ConversationId  = conversation.Id.ToString()
            };

            _logger.LogInformation(
                "[ChatService] Calling AI — userId={UserId} role={Role} conversationId={ConvId}",
                userId, role, conversation.Id);

            var aiResponse = await _aiService.SendChatMessageAsync(aiRequest);

            // ── 5. Save AI Response ───────────────────────────────────────────
            var responseText = string.IsNullOrWhiteSpace(aiResponse.Response)
                ? "I'm sorry, I couldn't process your request. Please try again."
                : aiResponse.Response;

            var aiMsg = new ChatMessage
            {
                ConversationId = messageDto.ConversationId,
                Content        = responseText,
                Sender         = "assistant",
                IsFallback     = aiResponse.IsFallback,
                Intent         = aiResponse.IntentExecuted,
                CreatedAt      = DateTime.UtcNow
            };
            _context.ChatMessages.Add(aiMsg);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[ChatService] AI response saved — intent={Intent} model={Model}",
                aiResponse.IntentExecuted, aiResponse.ModelUsed);

            // ── 6. Return ─────────────────────────────────────────────────────
            return new ChatResponseDto
            {
                Id            = aiMsg.Id,
                Content       = aiMsg.Content,
                Sender        = aiMsg.Sender,
                IsFallback    = aiMsg.IsFallback,
                SentAt        = aiMsg.CreatedAt
            };
        }

        // ── Academic Context Enrichment ──────────────────────────────────────

        /// <summary>
        /// Builds a role-specific academic context dictionary that FastAPI's
        /// PlannerAgent uses to auto-fill parameters from user context.
        ///
        /// Fields populated:
        ///   All roles:  userId, role
        ///   Students:   studentId, collegeId, departmentId, batchId, groupId,
        ///               enrolledOfferingIds (active)
        ///   Doctors:    doctorId, departmentId, assignedOfferingIds (active)
        /// </summary>
        private async Task<object> BuildAcademicContextAsync(Ulid userId, string role, string? profileId = null)
        {
            try
            {
                // Always include: userId + role
                var baseCtx = new Dictionary<string, object?>
                {
                    ["userId"] = userId.ToString(),
                    ["role"]   = role
                };

                if (role == "student")
                {
                    return await BuildStudentContextAsync(userId, baseCtx);
                }
                else if (role == "doctor")
                {
                    return await BuildDoctorContextAsync(userId, baseCtx);
                }

                // admin / superadmin — include profileId so FastAPI can call /api/Admins/{profileId}
                if (!string.IsNullOrEmpty(profileId))
                    baseCtx["profileId"] = profileId;
                return baseCtx;
            }
            catch (Exception ex)
            {
                // Context enrichment is non-fatal: AI still works with base userId/role
                _logger.LogWarning(ex,
                    "[ChatService] Academic context enrichment failed for userId={UserId}. " +
                    "AI will proceed with base context only.", userId);

                return new { userId = userId.ToString(), role };
            }
        }

        private async Task<object> BuildStudentContextAsync(
            Ulid userId,
            Dictionary<string, object?> ctx)
        {
            // Resolve the Student record linked to this SystemUser
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.SystemUserId == userId && s.IsActive);

            if (student == null)
                return ctx; // Guest / no profile — return base context

            ctx["studentId"]    = student.Id.ToString();
            ctx["collegeId"]    = student.CollegeId.ToString();
            ctx["departmentId"] = student.DepartmentId.ToString();
            ctx["batchId"]      = student.BatchId.ToString();
            ctx["groupId"]      = student.GroupId.ToString();

            // Active enrolled subject offering IDs (last 10 — capped for payload size)
            var offeringIds = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .OrderByDescending(e => e.EnrolledAt)
                .Take(10)
                .Select(e => e.SubjectOfferingId.ToString())
                .ToListAsync();

            ctx["enrolledOfferingIds"] = offeringIds;

            return ctx;
        }

        private async Task<object> BuildDoctorContextAsync(
            Ulid userId,
            Dictionary<string, object?> ctx)
        {
            // Resolve the Doctor record linked to this SystemUser
            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SystemUserId == userId);

            if (doctor == null)
                return ctx; // No profile — return base context

            ctx["doctorId"]    = doctor.Id.ToString();
            ctx["departmentId"] = doctor.DepartmentId.ToString();

            // Active subject offering IDs assigned to this doctor (last 10)
            var offeringIds = await _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.DoctorId == doctor.Id)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => o.Id.ToString())
                .ToListAsync();

            ctx["assignedOfferingIds"] = offeringIds;

            return ctx;
        }

        // ── Delete Message (Admin) ───────────────────────────────────────────

        public async Task DeleteMessageAsync(Ulid messageId)
        {
            var msg = await _context.ChatMessages.FindAsync(messageId);
            if (msg != null)
            {
                _context.ChatMessages.Remove(msg);
                await _context.SaveChangesAsync();
            }
        }
    }
}
