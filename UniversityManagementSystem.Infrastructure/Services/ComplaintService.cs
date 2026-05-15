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
                CreatedAt = c.CreatedAt
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
