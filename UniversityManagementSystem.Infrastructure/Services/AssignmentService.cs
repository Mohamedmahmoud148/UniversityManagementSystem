using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public AssignmentService(AppDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto, Ulid doctorId)
        {
            if (!Ulid.TryParse(dto.SubjectOfferingId, out var offeringId))
                throw new ArgumentException("Invalid SubjectOfferingId.");

            var offering = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .FirstOrDefaultAsync(o => o.Id == offeringId)
                ?? throw new KeyNotFoundException("Subject offering not found.");

            var assignment = new Assignment
            {
                Title = dto.Title,
                Description = dto.Description,
                SubjectOfferingId = offeringId,
                DoctorId = doctorId,
                Deadline = dto.Deadline,
                MaxGrade = dto.MaxGrade,
                AllowLateSubmission = dto.AllowLateSubmission,
                AiGradingEnabled = dto.AiGradingEnabled,
                GradingRubric = dto.GradingRubric,
                Code = $"ASN-{Ulid.NewUlid()}"
            };

            _context.Assignments.Add(assignment);
            await _context.SaveChangesAsync();

            return new AssignmentDto(
                assignment.Id.ToString(),
                assignment.Title,
                assignment.Description,
                offering.Subject.Name,
                assignment.Deadline,
                assignment.MaxGrade,
                assignment.AiGradingEnabled,
                0,
                assignment.CreatedAt);
        }

        public async Task<List<AssignmentDto>> GetOfferingAssignmentsAsync(Ulid offeringId)
        {
            var assignments = await _context.Assignments
                .Include(a => a.SubjectOffering).ThenInclude(o => o.Subject)
                .Where(a => a.SubjectOfferingId == offeringId)
                .ToListAsync();

            return assignments.Select(a => new AssignmentDto(
                a.Id.ToString(),
                a.Title,
                a.Description,
                a.SubjectOffering.Subject.Name,
                a.Deadline,
                a.MaxGrade,
                a.AiGradingEnabled,
                a.Submissions.Count,
                a.CreatedAt)).ToList();
        }

        public async Task<AssignmentDto> GetByIdAsync(Ulid id)
        {
            var a = await _context.Assignments
                .Include(x => x.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(x => x.Submissions)
                .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException("Assignment not found.");

            return new AssignmentDto(
                a.Id.ToString(),
                a.Title,
                a.Description,
                a.SubjectOffering.Subject.Name,
                a.Deadline,
                a.MaxGrade,
                a.AiGradingEnabled,
                a.Submissions.Count,
                a.CreatedAt);
        }

        public async Task<AssignmentSubmission> SubmitAsync(
            Ulid assignmentId, Ulid studentId,
            string? textAnswer, string? fileUrl, string? storageKey)
        {
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId)
                ?? throw new KeyNotFoundException("Assignment not found.");

            var now = DateTime.UtcNow;
            bool isLate = now > assignment.Deadline;

            if (isLate && !assignment.AllowLateSubmission)
                throw new InvalidOperationException("Deadline has passed and late submissions are not allowed.");

            var existing = await _context.AssignmentSubmissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (existing != null)
                throw new InvalidOperationException("You have already submitted this assignment.");

            var submission = new AssignmentSubmission
            {
                AssignmentId = assignmentId,
                StudentId = studentId,
                TextAnswer = textAnswer,
                FileUrl = fileUrl,
                StorageKey = storageKey,
                SubmittedAt = now,
                IsLate = isLate,
                Status = SubmissionStatus.Submitted,
                Code = $"SUB-{Ulid.NewUlid()}"
            };

            _context.AssignmentSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            return submission;
        }

        public async Task<List<SubmissionDto>> GetSubmissionsAsync(Ulid assignmentId, Ulid doctorId)
        {
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DoctorId == doctorId)
                ?? throw new UnauthorizedAccessException("Assignment not found or you are not the owner.");

            var submissions = await _context.AssignmentSubmissions
                .Include(s => s.Student)
                .Where(s => s.AssignmentId == assignmentId)
                .ToListAsync();

            return submissions.Select(MapToSubmissionDto).ToList();
        }

        public async Task<SubmissionDto> GradeManuallyAsync(
            Ulid submissionId, double grade, string? feedback, Ulid doctorId)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.Id == submissionId)
                ?? throw new KeyNotFoundException("Submission not found.");

            if (submission.Assignment.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the owner of this assignment.");

            submission.Grade = grade;
            submission.Feedback = feedback;
            submission.Status = SubmissionStatus.Graded;
            submission.IsHumanReviewed = true;
            submission.ReviewedByDoctorId = doctorId;

            await _context.SaveChangesAsync();

            return MapToSubmissionDto(submission);
        }

        public async Task<AiGradingResultDto> TriggerAiGradingAsync(Ulid submissionId)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.Id == submissionId)
                ?? throw new KeyNotFoundException("Submission not found.");

            var assignment = submission.Assignment;

            var requestBody = new
            {
                submission_text = submission.TextAnswer ?? string.Empty,
                assignment_title = assignment.Title,
                assignment_description = assignment.Description,
                rubric = assignment.GradingRubric,
                max_grade = assignment.MaxGrade
            };

            var fastApiClient = _httpClientFactory.CreateClient("FastApi");
            var response = await fastApiClient.PostAsJsonAsync("/api/ai/grade-submission", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"AI grading service error: {response.StatusCode} — {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<AiGradingResultDto>()
                ?? throw new InvalidOperationException("AI service returned an empty response.");

            // Persist AI grading result
            submission.Grade = result.Score;
            submission.AiFeedback = result.Feedback;
            submission.Strengths = result.Strengths;
            submission.Weaknesses = result.Weaknesses;
            submission.IsAiGraded = true;
            submission.Status = SubmissionStatus.Graded;

            await _context.SaveChangesAsync();

            return result;
        }

        public async Task<SubmissionDto?> GetStudentSubmissionAsync(Ulid assignmentId, Ulid studentId)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            return submission == null ? null : MapToSubmissionDto(submission);
        }

        public async Task DeleteAssignmentAsync(Ulid assignmentId, Ulid doctorId)
        {
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DoctorId == doctorId)
                ?? throw new UnauthorizedAccessException("Assignment not found or you are not the owner.");

            assignment.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static SubmissionDto MapToSubmissionDto(AssignmentSubmission s) =>
            new SubmissionDto(
                s.Id.ToString(),
                s.Student?.FullName ?? "Unknown",
                s.SubmittedAt,
                s.IsLate,
                s.Status,
                s.Grade,
                s.Feedback ?? s.AiFeedback,
                s.IsAiGraded,
                s.FileUrl,
                s.TextAnswer);
    }
}
