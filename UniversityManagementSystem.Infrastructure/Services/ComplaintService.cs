using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using Hangfire;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ComplaintService : IComplaintService
    {
        private readonly AppDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly INotificationService _notificationService;

        public ComplaintService(AppDbContext context, IBackgroundJobClient backgroundJobClient, INotificationService notificationService)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
            _notificationService = notificationService;
        }

        public async Task<ComplaintDto> CreateComplaintAsync(Ulid studentId, CreateComplaintDto dto)
        {
            var complaint = new Complaint
            {
                StudentId = studentId,
                Title = dto.Title,
                TargetType = dto.TargetType,
                TargetId = dto.TargetId ?? string.Empty,
                Message = dto.Message,
                Status = "Pending",
                Priority = "Normal",
                CreatedAt = DateTime.UtcNow
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            // Enqueue AI processing job
            _backgroundJobClient.Enqueue<IComplaintIntelligenceJob>(job => job.ProcessNewComplaintAsync(complaint.Id));

            // Notify doctor if the complaint targets a doctor
            if (dto.TargetType.Equals("Doctor", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(dto.TargetId)
                && Ulid.TryParse(dto.TargetId, out var doctorEntityId))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorEntityId && d.DeletedAt == null);
                if (doctor != null)
                {
                    try
                    {
                        await _notificationService.SendNotificationAsync(
                            doctor.SystemUserId,
                            "New Student Request",
                            "A student sent you a request requiring your response.");
                    }
                    catch
                    {
                        // Notification failure must not break complaint creation
                    }
                }
            }

            return MapToDto(complaint);
        }

        public async Task<ComplaintDto> ReplyToComplaintAsync(Ulid complaintId, string reply, Ulid doctorSystemUserId)
        {
            var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == complaintId);
            if (complaint == null) throw new KeyNotFoundException("Complaint not found.");

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.SystemUserId == doctorSystemUserId && d.DeletedAt == null);
            if (doctor == null) throw new UnauthorizedAccessException("Doctor profile not found.");

            if (!complaint.TargetType.Equals("Doctor", StringComparison.OrdinalIgnoreCase)
                || complaint.TargetId != doctor.Id.ToString())
            {
                throw new UnauthorizedAccessException("This complaint is not directed at you.");
            }

            complaint.ResolutionNote = reply;
            complaint.Status = "Resolved";
            complaint.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify the student
            try
            {
                await _notificationService.SendNotificationAsync(
                    complaint.StudentId,
                    "Reply to your request",
                    $"Your request '{complaint.Title}' has been answered.");
            }
            catch
            {
                // Notification failure must not break the reply
            }

            return MapToDto(complaint);
        }

        public async Task<ComplaintsPageDto> GetComplaintsAsync(GetComplaintsQueryDto query, Ulid callerId, string callerRole)
        {
            query.PageSize = Math.Clamp(query.PageSize, 1, 100);
            query.Page = Math.Max(query.Page, 1);

            var q = _context.Complaints
                .Include(c => c.Analysis)
                .AsNoTracking()
                .AsQueryable();

            // Role-based scoping
            if (callerRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(c => c.StudentId == callerId);
            }
            else if (callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                // Doctors see complaints targeting them or their subjects
                // Ensure doctor ID matches caller SystemUserId
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.SystemUserId == callerId);
                if (doctor == null) throw new UnauthorizedAccessException("Doctor profile not found.");

                q = q.Where(c => (c.TargetType == "Doctor" && c.TargetId == doctor.Id.ToString()) ||
                                 (c.TargetType == "SubjectOffering")); // They can filter further if needed
            }
            // Admin/SuperAdmin see all

            // Apply Filters
            if (query.From.HasValue) q = q.Where(c => c.CreatedAt >= query.From.Value);
            if (query.To.HasValue) q = q.Where(c => c.CreatedAt <= query.To.Value);
            if (!string.IsNullOrWhiteSpace(query.TargetType)) q = q.Where(c => c.TargetType == query.TargetType);
            if (!string.IsNullOrWhiteSpace(query.TargetId)) q = q.Where(c => c.TargetId == query.TargetId);
            if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(c => c.Status == query.Status);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(c => c.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            bool isAdmin = callerRole.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        || callerRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            bool maskStudent = callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase);

            // For admins: batch-load student profiles (one query, not N)
            Dictionary<Ulid, Student> studentProfiles = [];
            if (isAdmin)
            {
                var systemUserIds = items.Select(c => c.StudentId).Distinct().ToList();
                studentProfiles = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.SystemUser)
                    .Where(s => systemUserIds.Contains(s.SystemUserId) && s.DeletedAt == null)
                    .ToDictionaryAsync(s => s.SystemUserId);
            }

            var dtos = items.Select(c =>
            {
                studentProfiles.TryGetValue(c.StudentId, out var profile);
                return MapToDto(c, maskStudent: maskStudent, studentProfile: isAdmin ? profile : null);
            }).ToList();

            return new ComplaintsPageDto
            {
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize,
                Items = dtos
            };
        }

        public async Task<ComplaintDto> GetComplaintByIdAsync(Ulid complaintId, Ulid callerId, string callerRole)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Analysis)
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null) throw new KeyNotFoundException("Complaint not found.");

            // Scope checks
            if (callerRole.Equals("Student", StringComparison.OrdinalIgnoreCase) && complaint.StudentId != callerId)
            {
                throw new UnauthorizedAccessException();
            }

            if (callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.SystemUserId == callerId);
                if (doctor == null || (complaint.TargetType == "Doctor" && complaint.TargetId != doctor.Id.ToString()))
                {
                    throw new UnauthorizedAccessException();
                }
            }

            Student? studentProfile = null;
            bool isAdmin = callerRole.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        || callerRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            if (isAdmin)
            {
                studentProfile = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.SystemUser)
                    .FirstOrDefaultAsync(s => s.SystemUserId == complaint.StudentId && s.DeletedAt == null);
            }

            return MapToDto(complaint,
                maskStudent: callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase),
                studentProfile: studentProfile);
        }

        public async Task<List<DoctorOptionDto>> GetDoctorOptionsForStudentAsync(Ulid studentId)
        {
            // Resolve the student's college directly from the Student record.
            // Student.CollegeId is a direct FK — no need to traverse Group → Batch → Department.
            var collegeId = await _context.Students
                .AsNoTracking()
                .Where(s => s.Id == studentId && s.DeletedAt == null)
                .Select(s => (Ulid?)s.CollegeId)
                .FirstOrDefaultAsync();

            if (collegeId is null)
                return [];

            // Return all active doctors whose department belongs to that college.
            return await _context.Doctors
                .AsNoTracking()
                .Where(d => d.DeletedAt == null && d.Department.CollegeId == collegeId.Value)
                .OrderBy(d => d.FullName)
                .Select(d => new DoctorOptionDto(d.Id.ToString(), d.FullName))
                .ToListAsync();
        }

        public async Task<List<ComplaintClusterDto>> GetClustersAsync(string? targetType, string? targetId)
        {
            var q = _context.ComplaintClusters.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(targetType)) q = q.Where(c => c.TargetType == targetType);
            if (!string.IsNullOrWhiteSpace(targetId)) q = q.Where(c => c.TargetId == targetId);

            var clusters = await q.OrderByDescending(c => c.LastUpdated).ToListAsync();

            return clusters.Select(c => new ComplaintClusterDto
            {
                Id = c.Id.ToString(),
                Topic = c.Topic,
                TargetType = c.TargetType,
                TargetId = c.TargetId,
                ComplaintCount = c.ComplaintCount,
                AiSummary = c.AiSummary,
                LastUpdated = c.LastUpdated
            }).ToList();
        }

        public async Task<ClusterReplyResponseDto> ReplyToClusterAsync(Ulid clusterId, string message, Ulid repliedByUserId)
        {
            var cluster = await _context.ComplaintClusters
                .FirstOrDefaultAsync(c => c.Id == clusterId && c.DeletedAt == null);
            if (cluster == null) throw new KeyNotFoundException("Cluster not found.");

            // Get all complaints in this cluster via DuplicateGroupId
            var complaintIds = await _context.ComplaintAnalyses
                .Where(a => a.DuplicateGroupId == clusterId.ToString() && a.DeletedAt == null)
                .Select(a => a.ComplaintId)
                .ToListAsync();

            var complaints = await _context.Complaints
                .Where(c => complaintIds.Contains(c.Id) && c.DeletedAt == null)
                .ToListAsync();

            // Update each complaint
            foreach (var complaint in complaints)
            {
                complaint.ResolutionNote = message;
                complaint.Status = "Resolved";
                complaint.ResolvedAt = DateTime.UtcNow;
            }

            // Send individual notifications
            int notificationsSent = 0;
            var studentIds = complaints.Select(c => c.StudentId).Distinct().ToList();
            foreach (var studentId in studentIds)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        studentId,
                        "Response to your complaint",
                        $"A response has been issued for the topic: '{cluster.Topic}'. Message: {message}");
                    notificationsSent++;
                }
                catch { /* notification failure must not break the flow */ }
            }

            // Store reply history
            var reply = new ClusterReply
            {
                ClusterId = clusterId,
                RepliedByUserId = repliedByUserId,
                Message = message,
                AffectedStudents = studentIds.Count,
                NotificationsSent = notificationsSent,
                CreatedAt = DateTime.UtcNow
            };
            _context.ClusterReplies.Add(reply);

            // Update cluster status
            cluster.Status = "Resolved";
            cluster.ResolvedAt = DateTime.UtcNow;
            cluster.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ClusterReplyResponseDto
            {
                ClusterId = clusterId.ToString(),
                Topic = cluster.Topic,
                AffectedStudents = studentIds.Count,
                NotificationsSent = notificationsSent,
                Message = message,
                RepliedAt = DateTime.UtcNow
            };
        }

        public async Task<EnhancedComplaintClusterDto> GetClusterByIdAsync(Ulid clusterId)
        {
            var cluster = await _context.ComplaintClusters
                .Include(c => c.Replies)
                .Include(c => c.StatusHistory)
                .FirstOrDefaultAsync(c => c.Id == clusterId && c.DeletedAt == null);
            if (cluster == null) throw new KeyNotFoundException("Cluster not found.");
            return MapToEnhancedDto(cluster);
        }

        public async Task UpdateClusterStatusAsync(Ulid clusterId, string newStatus, Ulid changedByUserId, string? reason)
        {
            var validStatuses = new[] { "Open", "Investigating", "Resolved", "Archived" };
            if (!validStatuses.Contains(newStatus))
                throw new ArgumentException($"Invalid status. Valid: {string.Join(", ", validStatuses)}");

            var cluster = await _context.ComplaintClusters
                .FirstOrDefaultAsync(c => c.Id == clusterId && c.DeletedAt == null);
            if (cluster == null) throw new KeyNotFoundException("Cluster not found.");

            var history = new ClusterStatusHistory
            {
                ClusterId = clusterId,
                OldStatus = cluster.Status,
                NewStatus = newStatus,
                ChangedByUserId = changedByUserId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
            _context.ClusterStatusHistories.Add(history);

            cluster.Status = newStatus;
            if (newStatus == "Resolved") cluster.ResolvedAt = DateTime.UtcNow;
            cluster.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<ComplaintDashboardDto> GetDashboardAsync()
        {
            var since30Days = DateTime.UtcNow.AddDays(-30);

            var totalComplaints = await _context.Complaints.CountAsync(c => c.DeletedAt == null);
            var pending = await _context.Complaints.CountAsync(c => c.DeletedAt == null && c.Status == "Pending");
            var underReview = await _context.Complaints.CountAsync(c => c.DeletedAt == null && c.Status == "UnderReview");
            var resolved = await _context.Complaints.CountAsync(c => c.DeletedAt == null && c.Status == "Resolved");
            var dismissed = await _context.Complaints.CountAsync(c => c.DeletedAt == null && c.Status == "Dismissed");
            var critical = await _context.Complaints.CountAsync(c => c.DeletedAt == null && c.Priority == "Critical");
            var totalClusters = await _context.ComplaintClusters.CountAsync(c => c.DeletedAt == null);

            var categories = await _context.ComplaintAnalyses
                .Where(a => a.DeletedAt == null && a.Category != null)
                .GroupBy(a => a.Category)
                .Select(g => new CategoryCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            var severities = await _context.ComplaintAnalyses
                .Where(a => a.DeletedAt == null && a.Severity != null)
                .GroupBy(a => a.Severity)
                .Select(g => new SeverityCountDto { Severity = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var topClusters = await _context.ComplaintClusters
                .Where(c => c.DeletedAt == null)
                .OrderByDescending(c => c.ComplaintCount)
                .Take(5)
                .ToListAsync();

            // Average resolution time (hours)
            var resolvedComplaints = await _context.Complaints
                .Where(c => c.DeletedAt == null && c.Status == "Resolved" && c.ResolvedAt != null)
                .Select(c => new { c.CreatedAt, c.ResolvedAt })
                .ToListAsync();

            double avgResolutionHours = resolvedComplaints.Any()
                ? resolvedComplaints.Average(c => (c.ResolvedAt!.Value - c.CreatedAt).TotalHours)
                : 0;

            double avgSentiment = await _context.ComplaintAnalyses
                .Where(a => a.DeletedAt == null)
                .Select(a => (double?)a.SentimentScore)
                .AverageAsync() ?? 0;

            int trendingCount = await _context.ComplaintClusters
                .CountAsync(c => c.DeletedAt == null && c.TrendDirection == "Increasing");

            // Complaints over time (last 30 days)
            var overTime = await _context.Complaints
                .Where(c => c.DeletedAt == null && c.CreatedAt >= since30Days)
                .GroupBy(c => c.CreatedAt.Date)
                .Select(g => new DailyComplaintCountDto { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return new ComplaintDashboardDto
            {
                Summary = new ComplaintSummaryDto
                {
                    TotalComplaints = totalComplaints,
                    Pending = pending,
                    UnderReview = underReview,
                    Resolved = resolved,
                    Dismissed = dismissed,
                    Critical = critical,
                    TotalClusters = totalClusters
                },
                Categories = categories,
                Severities = severities,
                TopClusters = topClusters.Select(MapToEnhancedDto).ToList(),
                Metrics = new ComplaintMetricsDto
                {
                    AverageResolutionHours = Math.Round(avgResolutionHours, 1),
                    AverageSentiment = Math.Round(avgSentiment, 3),
                    TrendingClustersCount = trendingCount
                },
                OverTime = overTime
            };
        }

        private static EnhancedComplaintClusterDto MapToEnhancedDto(ComplaintCluster c)
        {
            List<string> recommendations = new();
            if (!string.IsNullOrWhiteSpace(c.AiRecommendations))
            {
                try { recommendations = System.Text.Json.JsonSerializer.Deserialize<List<string>>(c.AiRecommendations) ?? new(); }
                catch { }
            }

            return new EnhancedComplaintClusterDto
            {
                Id = c.Id.ToString(),
                Topic = c.Topic,
                TargetType = c.TargetType,
                TargetId = c.TargetId,
                ComplaintCount = c.ComplaintCount,
                CriticalCount = c.CriticalCount,
                AiSummary = c.AiSummary,
                AiRecommendations = recommendations,
                Status = c.Status,
                TrendDirection = c.TrendDirection,
                AverageSentiment = c.AverageSentiment,
                FirstComplaintAt = c.FirstComplaintAt,
                LastUpdated = c.LastUpdated,
                ResolvedAt = c.ResolvedAt,
                Replies = (c.Replies ?? new()).Select(r => new ClusterReplyDto
                {
                    Id = r.Id.ToString(),
                    Message = r.Message,
                    AffectedStudents = r.AffectedStudents,
                    NotificationsSent = r.NotificationsSent,
                    RepliedAt = r.CreatedAt
                }).ToList(),
                StatusHistory = (c.StatusHistory ?? new()).Select(h => new ClusterStatusHistoryDto
                {
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    Reason = h.Reason,
                    ChangedAt = h.CreatedAt
                }).OrderByDescending(h => h.ChangedAt).ToList()
            };
        }

        private static ComplaintDto MapToDto(Complaint c, bool maskStudent = false, Student? studentProfile = null)
        {
            var dto = new ComplaintDto
            {
                Id = c.Id.ToString(),
                StudentId = maskStudent ? "HIDDEN" : c.StudentId.ToString(),
                Title = c.Title,
                TargetType = c.TargetType,
                TargetId = c.TargetId,
                Message = c.Message,
                Status = c.Status,
                Priority = c.Priority,
                ResolutionNote = c.ResolutionNote,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt
            };

            if (studentProfile != null)
            {
                dto.Student = new ComplaintStudentDto
                {
                    Id = studentProfile.Id.ToString(),
                    FullName = studentProfile.FullName,
                    NationalId = studentProfile.SystemUser?.NationalId ?? string.Empty,
                    Email = studentProfile.Email,
                    PhoneNumber = studentProfile.Phone,
                    AcademicCode = studentProfile.UniversityStudentId
                };
            }

            if (c.Analysis != null)
            {
                dto.Analysis = new ComplaintAnalysisDto
                {
                    SentimentScore = c.Analysis.SentimentScore,
                    Category = c.Analysis.Category,
                    Severity = c.Analysis.Severity,
                    AiSummary = c.Analysis.AiSummary,
                    SuggestedAction = c.Analysis.SuggestedAction
                };
            }

            return dto;
        }
    }
}
