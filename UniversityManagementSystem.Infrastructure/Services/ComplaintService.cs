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

        public ComplaintService(AppDbContext context, IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
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

            var dtos = items.Select(c => MapToDto(c, maskStudent: callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase))).ToList();

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

            return MapToDto(complaint, maskStudent: callerRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase));
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

        private static ComplaintDto MapToDto(Complaint c, bool maskStudent = false)
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
                CreatedAt = c.CreatedAt
            };

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
