using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Smart registration engine.
    /// Runs 7 ordered validation policies before allowing any enrollment.
    /// Integrates with the existing EnrollmentService — does NOT replace it.
    /// </summary>
    public class RegistrationService(
        AppDbContext context,
        IAcademicStatusService statusService,
        IAuditService auditService) : IRegistrationService
    {
        // ─────────────────────────────────────────────────────────────────────
        // GET ELIGIBLE OFFERINGS
        // Returns all offerings for a semester annotated with eligibility info.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<IReadOnlyList<EligibleOfferingDto>> GetEligibleOfferingsAsync(
            Ulid studentId, Ulid semesterId)
        {
            var student = await context.Students
                .AsNoTracking()
                .Include(s => s.Batch)
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == studentId && s.DeletedAt == null)
                ?? throw new KeyNotFoundException("Student not found.");

            var status = await statusService.GetOrCreateAsync(studentId);
            var policy = await statusService.GetPolicyAsync(student.DepartmentId);

            // All offerings for this semester matching the student's batch/department
            var offerings = await context.SubjectOfferings
                .AsNoTracking()
                .Include(o => o.Subject)
                .Include(o => o.Doctor)
                .Include(o => o.Semester)
                .Where(o =>
                    o.SemesterId    == semesterId &&
                    o.DepartmentId  == student.DepartmentId &&
                    o.BatchId       == student.BatchId &&
                    (o.GroupId == null || o.GroupId == student.GroupId) &&
                    o.DeletedAt == null)
                .ToListAsync();

            if (offerings.Count == 0) return Array.Empty<EligibleOfferingDto>();

            var offeringIds   = offerings.Select(o => o.Id).ToList();
            var subjectIds    = offerings.Select(o => o.SubjectId).ToList();

            // Pre-fetch: enrolled count per offering
            var enrolledCounts = await context.Enrollments
                .Where(e => offeringIds.Contains(e.SubjectOfferingId)
                         && e.IsActive && e.DeletedAt == null)
                .GroupBy(e => e.SubjectOfferingId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            // Pre-fetch: waitlist count per offering
            var waitlistCounts = await context.SubjectOfferingWaitlists
                .Where(w => offeringIds.Contains(w.SubjectOfferingId) && w.DeletedAt == null)
                .GroupBy(w => w.SubjectOfferingId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            // Pre-fetch: student's current active enrollments for this semester
            var currentSemesterHours = await context.Enrollments
                .Where(e => e.StudentId == studentId
                         && e.IsActive
                         && e.DeletedAt == null
                         && e.SubjectOffering.SemesterId == semesterId)
                .SumAsync(e => (int?)e.SubjectOffering.Subject.CreditHours) ?? 0;

            // Pre-fetch: student's finalized grades (for passed-subject and prereq checks)
            var passedSubjectIds = await context.StudentGrades
                .Where(g => g.StudentId == studentId
                         && g.IsFinalized
                         && g.GradePoints >= 1.0   // D or above = passing
                         && g.DeletedAt == null)
                .Select(g => g.SubjectOffering.SubjectId)
                .ToHashSetAsync();

            // All finalized grades (for prereq minimum-grade check)
            var allGrades = await context.StudentGrades
                .AsNoTracking()
                .Include(g => g.SubjectOffering)
                .Where(g => g.StudentId == studentId && g.IsFinalized && g.DeletedAt == null)
                .ToListAsync();

            // Pre-fetch: prerequisites for all relevant subjects
            var prerequisites = await context.SubjectPrerequisites
                .AsNoTracking()
                .Include(p => p.PrerequisiteSubject)
                .Where(p => subjectIds.Contains(p.SubjectId) && p.DeletedAt == null)
                .ToListAsync();

            // Pre-fetch: student's existing enrollments (including this semester)
            var activeEnrollments = await context.Enrollments
                .Where(e => e.StudentId == studentId && e.IsActive && e.DeletedAt == null)
                .Select(e => e.SubjectOfferingId)
                .ToHashSetAsync();

            int maxAllowedHours = ComputeMaxHours(status.GPA, policy);

            var result = new List<EligibleOfferingDto>();

            foreach (var offering in offerings)
            {
                var blockers = new List<string>();
                var warnings = new List<string>();

                int enrolled   = enrolledCounts.GetValueOrDefault(offering.Id);
                int waitlisted = waitlistCounts.GetValueOrDefault(offering.Id);
                bool isFull    = enrolled >= offering.MaxCapacity;

                // ── Policy 1: Already enrolled in this offering ──────────────
                if (activeEnrollments.Contains(offering.Id))
                {
                    blockers.Add("You are already enrolled in this offering.");
                    goto done;
                }

                // ── Policy 2: Already passed this subject ────────────────────
                if (passedSubjectIds.Contains(offering.SubjectId))
                {
                    blockers.Add("You have already passed this subject.");
                    goto done;
                }

                // ── Policy 3: Prerequisites ──────────────────────────────────
                var subjectPrereqs = prerequisites
                    .Where(p => p.SubjectId == offering.SubjectId)
                    .ToList();

                foreach (var prereq in subjectPrereqs)
                {
                    var prereqGrade = allGrades
                        .Where(g => g.SubjectOffering.SubjectId == prereq.PrerequisiteSubjectId)
                        .OrderByDescending(g => g.CalculatedAt)
                        .FirstOrDefault();

                    if (prereqGrade == null)
                    {
                        blockers.Add($"Prerequisite not completed: {prereq.PrerequisiteSubject.Name}");
                        continue;
                    }

                    if (prereqGrade.GradePoints < 1.0)
                    {
                        blockers.Add(
                            $"Must PASS prerequisite: {prereq.PrerequisiteSubject.Name} " +
                            $"(currently: {prereqGrade.GradeLetter})");
                        continue;
                    }

                    if (prereq.MinimumGrade.HasValue && prereqGrade.FinalScore < prereq.MinimumGrade.Value)
                    {
                        blockers.Add(
                            $"Minimum score {prereq.MinimumGrade.Value} required in " +
                            $"{prereq.PrerequisiteSubject.Name} (scored {prereqGrade.FinalScore:F1})");
                    }
                }

                if (blockers.Count > 0) goto done;

                // ── Policy 4: GPA-based academic standing ────────────────────
                if (status.Standing == AcademicStanding.Probation && status.GPA < policy.ProbationGpaThreshold)
                    warnings.Add($"⚠️ Probation: GPA {status.GPA:F2} < {policy.ProbationGpaThreshold}. Max {policy.ProbationMaxHours} hours.");

                if (status.Standing == AcademicStanding.Warning)
                    warnings.Add($"⚠️ Academic Warning: GPA {status.GPA:F2}. Max {policy.WarningMaxHours} hours.");

                // ── Policy 5: Credit hours limit ─────────────────────────────
                int projectedHours = currentSemesterHours + offering.Subject.CreditHours;
                if (projectedHours > maxAllowedHours)
                {
                    blockers.Add(
                        $"Credit hours limit exceeded: {currentSemesterHours} registered + " +
                        $"{offering.Subject.CreditHours} = {projectedHours} > max {maxAllowedHours} " +
                        $"(GPA: {status.GPA:F2})");
                    goto done;
                }

                // ── Policy 6: Capacity check → warn, don't block (waitlist available) ─
                if (isFull)
                    warnings.Add($"Offering is full ({enrolled}/{offering.MaxCapacity}). You can join the waitlist (position {waitlisted + 1}).");

                // ── Policy 7: General academic standing suspension ────────────
                if (status.Standing == AcademicStanding.Suspended)
                {
                    blockers.Add("Your account is suspended. Contact academic affairs.");
                    goto done;
                }

                done:
                result.Add(new EligibleOfferingDto
                {
                    OfferingId    = offering.Id.ToString(),
                    SubjectName   = offering.Subject?.Name ?? "",
                    SubjectCode   = offering.Subject?.Code ?? "",
                    CreditHours   = offering.Subject?.CreditHours ?? 0,
                    DoctorName    = offering.Doctor?.FullName ?? "",
                    SemesterName  = offering.Semester?.Name ?? "",
                    MaxCapacity   = offering.MaxCapacity,
                    EnrolledCount = enrolled,
                    WaitlistCount = waitlisted,
                    IsFull        = isFull,
                    IsEligible    = blockers.Count == 0,
                    Blockers      = blockers,
                    Warnings      = warnings
                });
            }

            // Show eligible offerings first, then blocked ones
            return result.OrderBy(r => !r.IsEligible).ThenBy(r => r.SubjectName).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // ENROLL
        // ─────────────────────────────────────────────────────────────────────
        public async Task<EnrollmentResultDto> EnrollAsync(Ulid studentId, Ulid offeringId)
        {
            var offering = await context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Semester)
                .FirstOrDefaultAsync(o => o.Id == offeringId && o.DeletedAt == null)
                ?? throw new KeyNotFoundException("Subject offering not found.");

            // Run eligibility check for this one offering
            var eligible = await GetEligibleOfferingsAsync(studentId, offering.SemesterId);
            var thisOne  = eligible.FirstOrDefault(e => e.OfferingId == offeringId.ToString());

            if (thisOne == null)
                return new EnrollmentResultDto { Success = false, Errors = ["Offering not found in eligible list."] };

            if (thisOne.Blockers.Count > 0)
                return new EnrollmentResultDto { Success = false, Errors = thisOne.Blockers, Warnings = thisOne.Warnings };

            // If offering is full → auto-add to waitlist
            if (thisOne.IsFull)
            {
                var waitlistResult = await JoinWaitlistAsync(studentId, offeringId);
                return new EnrollmentResultDto
                {
                    Success         = false,
                    AddedToWaitlist = true,
                    WaitlistPosition = waitlistResult.Position,
                    Message  = waitlistResult.Message,
                    Warnings = thisOne.Warnings
                };
            }

            // Check for existing enrollment (including soft-deleted)
            var existing = await context.Enrollments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.SubjectOfferingId == offeringId);

            if (existing?.IsActive == true && existing.DeletedAt == null)
                return new EnrollmentResultDto { Success = false, Errors = ["Already enrolled."] };

            if (existing != null)
            {
                existing.IsActive   = true;
                existing.DeletedAt  = null;
                existing.EnrolledAt = DateTime.UtcNow;
            }
            else
            {
                context.Enrollments.Add(new Enrollment
                {
                    StudentId         = studentId,
                    SubjectOfferingId = offeringId,
                    IsActive          = true
                });
            }

            await context.SaveChangesAsync();

            await auditService.LogAsync("Enroll", "Enrollment",
                $"{studentId}:{offeringId}", null,
                System.Text.Json.JsonSerializer.Serialize(new { studentId, offeringId }),
                studentId);

            return new EnrollmentResultDto
            {
                Success  = true,
                Message  = $"Successfully enrolled in {offering.Subject?.Name}.",
                Warnings = thisOne.Warnings
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // WAITLIST
        // ─────────────────────────────────────────────────────────────────────
        public async Task<WaitlistResultDto> JoinWaitlistAsync(Ulid studentId, Ulid offeringId)
        {
            // Check not already on waitlist
            var existing = await context.SubjectOfferingWaitlists
                .FirstOrDefaultAsync(w => w.StudentId == studentId
                                       && w.SubjectOfferingId == offeringId
                                       && w.DeletedAt == null);

            if (existing != null)
                return new WaitlistResultDto
                {
                    Success  = true,
                    Position = existing.Position,
                    Message  = $"Already on waitlist at position {existing.Position}."
                };

            // Determine next position
            var nextPos = await context.SubjectOfferingWaitlists
                .Where(w => w.SubjectOfferingId == offeringId && w.DeletedAt == null)
                .CountAsync() + 1;

            context.SubjectOfferingWaitlists.Add(new SubjectOfferingWaitlist
            {
                StudentId         = studentId,
                SubjectOfferingId = offeringId,
                Position          = nextPos
            });

            await context.SaveChangesAsync();

            return new WaitlistResultDto
            {
                Success  = true,
                Position = nextPos,
                Message  = $"Added to waitlist at position {nextPos}. You will be notified when a spot opens."
            };
        }

        public async Task LeaveWaitlistAsync(Ulid studentId, Ulid offeringId)
        {
            var entry = await context.SubjectOfferingWaitlists
                .FirstOrDefaultAsync(w => w.StudentId == studentId
                                       && w.SubjectOfferingId == offeringId
                                       && w.DeletedAt == null)
                ?? throw new KeyNotFoundException("Waitlist entry not found.");

            entry.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // Re-number remaining positions
            var remaining = await context.SubjectOfferingWaitlists
                .Where(w => w.SubjectOfferingId == offeringId
                         && w.Position > entry.Position
                         && w.DeletedAt == null)
                .OrderBy(w => w.Position)
                .ToListAsync();

            foreach (var w in remaining) w.Position--;
            await context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private static int ComputeMaxHours(double gpa, AcademicPolicy policy) => gpa switch
        {
            var g when g >= policy.HonorGpaThreshold      => policy.HonorMaxHours,
            var g when g < policy.ProbationGpaThreshold   => policy.ProbationMaxHours,
            var g when g < policy.WarningGpaThreshold      => policy.WarningMaxHours,
            _                                              => policy.DefaultMaxHours
        };
    }
}
