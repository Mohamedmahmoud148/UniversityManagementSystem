using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services.Deletion
{
    public class DeletionService(AppDbContext context, IAuditService auditService) : IDeletionService
    {
        private static readonly JsonSerializerOptions _snapshotOptions = new()
        {
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        // ── Public: Analyze ──────────────────────────────────────────────────

        public async Task<DeleteAnalysisResponseDto> AnalyzeAsync(string entityName, Ulid entityId)
        {
            var policy  = EntityPolicies.GetPolicy(entityName);
            var display = await GetDisplayNameAsync(entityName, entityId);

            var summary  = new Dictionary<string, int>();
            var warnings = new List<string>();
            var blockers = new List<DeleteBlockerDto>();
            var tree     = new List<DependencyNodeDto>();

            // Recursively scan dependency graph
            await ScanDependenciesAsync(entityName, entityId, summary, tree, blockers, depth: 0);

            // Check immutability blockers
            await CheckImmutabilityAsync(entityName, entityId, blockers);

            // Build warnings from policy + blockers + summary
            BuildWarnings(entityName, policy, summary, blockers, warnings);

            var isBlocked  = blockers.Count > 0;
            var canDelete  = !isBlocked;
            var confirm    = BuildConfirmationRequirements(policy, display);

            // Calculate deletion order for this entity's subtree
            var deletionOrder = BuildDeletionOrder(entityName, summary);

            return new DeleteAnalysisResponseDto
            {
                EntityName     = entityName,
                EntityId       = entityId.ToString(),
                DisplayName    = display,
                RiskLevel      = policy.RiskLevel,
                RiskLevelLabel = policy.RiskLevel.ToString().ToUpper(),
                DeleteType     = policy.DeleteType,
                DeleteTypeLabel = policy.DeleteType switch
                {
                    DeleteType.SoftDelete       => "Soft Delete",
                    DeleteType.HardDelete       => "Hard Delete",
                    DeleteType.Restricted       => "Restricted",
                    DeleteType.ArchiveOnly      => "Archive Only",
                    DeleteType.ImmutableBlocked => "Immutable — Cannot Delete",
                    _                           => "Unknown"
                },
                CanDelete      = canDelete,
                IsBlocked      = isBlocked,
                Summary        = new AffectedSummaryDto { Counts = summary },
                DependencyTree = tree,
                Warnings       = warnings,
                Blockers       = blockers,
                Confirmation   = confirm,
                DeletionOrder  = deletionOrder
            };
        }

        // ── Public: Execute ──────────────────────────────────────────────────

        public async Task<DeleteExecutionResponseDto> ExecuteAsync(
            DeleteExecutionRequestDto request,
            Ulid performedByUserId)
        {
            if (!Ulid.TryParse(request.EntityId, out var entityId))
                throw new ArgumentException("Invalid entity ID format.");

            var policy = EntityPolicies.GetPolicy(request.EntityName);

            // ── Guard: Immutable ─────────────────────────────────────────────
            if (policy.DeleteType == DeleteType.ImmutableBlocked)
                throw new InvalidOperationException(
                    $"{policy.FriendlyName} cannot be deleted. It is immutable.");

            // ── Guard: Typed confirmation ────────────────────────────────────
            if (policy.RiskLevel >= DeleteRiskLevel.High && policy.ConfirmationPhrase != null)
            {
                var expected = policy.ConfirmationPhrase.ToUpperInvariant();
                var provided = (request.TypedConfirmationPhrase ?? "").ToUpperInvariant().Trim();
                if (provided != expected)
                    throw new InvalidOperationException(
                        $"Typed confirmation does not match. Expected: \"{expected}\"");
            }

            // ── Guard: Re-check blockers ─────────────────────────────────────
            var blockers = new List<DeleteBlockerDto>();
            await CheckImmutabilityAsync(request.EntityName, entityId, blockers);
            if (blockers.Count > 0)
                throw new InvalidOperationException(
                    $"Delete blocked: {string.Join("; ", blockers.Select(b => b.Reason))}");

            // ── Capture old values for audit ─────────────────────────────────
            var oldValues = await GetEntitySnapshotAsync(request.EntityName, entityId);

            // ── Execute leaf-first within a transaction ───────────────────────
            var executedSteps  = new List<string>();
            var affectedCounts = new Dictionary<string, int>();

            await using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                await ExecuteDeleteAsync(
                    request.EntityName, entityId, policy,
                    executedSteps, affectedCounts);

                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            // ── Audit log ────────────────────────────────────────────────────
            await auditService.LogAsync(
                actionType:          "Delete",
                entityName:          request.EntityName,
                entityId:            entityId.ToString(),
                oldValues:           oldValues,
                newValues:           JsonSerializer.Serialize(new { DeleteType = policy.DeleteType.ToString() }),
                performedByUserId:   performedByUserId);

            return new DeleteExecutionResponseDto
            {
                Success           = true,
                Message           = $"{policy.FriendlyName} has been successfully {(policy.DeleteType == DeleteType.SoftDelete ? "deactivated (soft deleted)" : "deleted")}.",
                EntityName        = request.EntityName,
                EntityId          = request.EntityId,
                DeleteTypeApplied = policy.DeleteType,
                AffectedCounts    = affectedCounts,
                ExecutedSteps     = executedSteps
            };
        }

        // ── Recursive dependency scanner ─────────────────────────────────────

        private async Task ScanDependenciesAsync(
            string entityName,
            Ulid entityId,
            Dictionary<string, int> summary,
            List<DependencyNodeDto> tree,
            List<DeleteBlockerDto> blockers,
            int depth)
        {
            if (depth > 6) return; // Guard against runaway recursion

            if (!DependencyGraph.Children.TryGetValue(entityName, out var children))
                return;

            foreach (var child in children)
            {
                var count = await CountChildrenAsync(entityName, entityId, child.ChildEntity);
                if (count == 0) continue;

                // Accumulate into flat summary
                if (summary.ContainsKey(child.ChildEntity))
                    summary[child.ChildEntity] += count;
                else
                    summary[child.ChildEntity] = count;

                // Build blocker if this child is blocking
                if (child.IsBlocking && child.DeleteBehavior == "Restrict")
                {
                    var childPolicy = EntityPolicies.GetPolicy(child.ChildEntity);
                    if (childPolicy.IsHistorical || childPolicy.DeleteType == DeleteType.Restricted)
                    {
                        blockers.Add(new DeleteBlockerDto
                        {
                            Reason     = $"Has {count} {child.FriendlyName} that contain historical data",
                            EntityName = child.ChildEntity,
                            Count      = count
                        });
                    }
                }

                // Build tree node
                var node = new DependencyNodeDto
                {
                    EntityName     = child.ChildEntity,
                    FriendlyName   = child.FriendlyName,
                    Count          = count,
                    IsHistorical   = child.IsHistorical,
                    IsBlocking     = child.IsBlocking,
                    DeleteBehavior = child.DeleteBehavior
                };

                // Recurse into grandchildren (limited depth to avoid O(n!) scans)
                if (depth < 3 && DependencyGraph.Children.ContainsKey(child.ChildEntity))
                {
                    // Use first entity of child to recurse — this is a heuristic scan
                    var sampleChildId = await GetFirstChildIdAsync(entityName, entityId, child.ChildEntity);
                    if (sampleChildId.HasValue)
                    {
                        var grandchildSummary = new Dictionary<string, int>();
                        var grandchildBlockers = new List<DeleteBlockerDto>();
                        await ScanDependenciesAsync(
                            child.ChildEntity, sampleChildId.Value,
                            grandchildSummary, node.Children, grandchildBlockers, depth + 1);

                        // Scale up grandchild estimates by child count
                        foreach (var (k, v) in grandchildSummary)
                        {
                            var scaled = v * count;
                            if (summary.ContainsKey(k)) summary[k] += scaled;
                            else summary[k] = scaled;
                        }
                        // Propagate historical blockers upward
                        blockers.AddRange(grandchildBlockers
                            .Where(b => !blockers.Any(x => x.EntityName == b.EntityName)));
                    }
                }

                tree.Add(node);
            }
        }

        // ── Immutability checks ───────────────────────────────────────────────

        private async Task CheckImmutabilityAsync(
            string entityName,
            Ulid entityId,
            List<DeleteBlockerDto> blockers)
        {
            switch (entityName)
            {
                case "StudentGrade":
                    var grade = await context.StudentGrades.FindAsync(entityId);
                    if (grade?.IsFinalized == true)
                        blockers.Add(new() { Reason = "Grade is finalized and immutable", EntityName = "StudentGrade", Count = 1 });
                    break;

                case "Exam":
                    var exam = await context.Exams.FindAsync(entityId);
                    if (exam != null && exam.Status != UniversityManagementSystem.Core.Entities.ExamStatus.Draft)
                        blockers.Add(new() { Reason = $"Exam is {exam.Status} — only Draft exams can be deleted", EntityName = "Exam", Count = 1 });
                    // Also block if submissions exist
                    var submissionCount = await context.ExamSubmissions.CountAsync(s => s.ExamId == entityId);
                    if (submissionCount > 0)
                        blockers.Add(new() { Reason = $"Exam has {submissionCount} student submissions", EntityName = "ExamSubmission", Count = submissionCount });
                    break;

                case "Enrollment":
                    var enrollment = await context.Enrollments.FindAsync(entityId);
                    if (enrollment?.IsActive == true)
                        blockers.Add(new() { Reason = "Active enrollment cannot be deleted", EntityName = "Enrollment", Count = 1 });
                    break;

                case "AuditLog":
                    blockers.Add(new() { Reason = "Audit logs are immutable — they can never be deleted", EntityName = "AuditLog", Count = 1 });
                    break;

                case "Semester":
                    // Block if any SubjectOffering has active enrollments
                    var activeEnrollments = await context.Enrollments
                        .CountAsync(e => e.SubjectOffering.SemesterId == entityId && e.IsActive);
                    if (activeEnrollments > 0)
                        blockers.Add(new() { Reason = $"Semester has {activeEnrollments} active enrollments", EntityName = "Enrollment", Count = activeEnrollments });
                    break;

                case "SubjectOffering":
                    var finalizedGrades = await context.StudentGrades
                        .CountAsync(g => g.SubjectOfferingId == entityId && g.IsFinalized);
                    if (finalizedGrades > 0)
                        blockers.Add(new() { Reason = $"Subject offering has {finalizedGrades} finalized grades", EntityName = "StudentGrade", Count = finalizedGrades });
                    break;

                case "Regulation":
                    var batchesUsing = await context.Batches.CountAsync(b => b.RegulationId == entityId);
                    if (batchesUsing > 0)
                        blockers.Add(new() { Reason = $"Regulation is assigned to {batchesUsing} batches", EntityName = "Batch", Count = batchesUsing });
                    break;
            }
        }

        // ── Count helpers (EF Core per-entity) ───────────────────────────────

        private async Task<int> CountChildrenAsync(string parentEntity, Ulid parentId, string childEntity)
        {
            return (parentEntity, childEntity) switch
            {
                ("University",      "College")              => await context.Colleges.CountAsync(x => x.UniversityId == parentId),
                ("University",      "Student")              => await context.Students.CountAsync(x => x.UniversityId == parentId),
                ("College",         "Department")           => await context.Departments.CountAsync(x => x.CollegeId == parentId),
                ("College",         "AcademicYear")         => await context.AcademicYears.CountAsync(x => x.CollegeId == parentId),
                ("College",         "Subject")              => await context.Subjects.CountAsync(x => x.CollegeId == parentId),
                ("College",         "Student")              => await context.Students.CountAsync(x => x.CollegeId == parentId),
                ("Department",      "Batch")                => await context.Batches.CountAsync(x => x.DepartmentId == parentId),
                ("Department",      "Doctor")               => await context.Doctors.CountAsync(x => x.DepartmentId == parentId),
                ("Department",      "TeachingAssistant")    => await context.TeachingAssistants.CountAsync(x => x.DepartmentId == parentId),
                ("Department",      "Subject")              => await context.Subjects.CountAsync(x => x.DepartmentId == parentId),
                ("Department",      "Regulation")           => await context.Regulations.CountAsync(x => x.DepartmentId == parentId),
                ("Department",      "AcademicYearDepartment") => await context.AcademicYearDepartments.CountAsync(x => x.DepartmentId == parentId),
                ("AcademicYear",    "Semester")             => await context.Semesters.CountAsync(x => x.AcademicYearId == parentId),
                ("AcademicYear",    "AcademicYearDepartment") => await context.AcademicYearDepartments.CountAsync(x => x.AcademicYearId == parentId),
                ("Batch",           "Group")                => await context.Groups.CountAsync(x => x.BatchId == parentId),
                ("Batch",           "Student")              => await context.Students.CountAsync(x => x.BatchId == parentId),
                ("Batch",           "ScheduleEntry")        => await context.ScheduleEntries.CountAsync(x => x.BatchId == parentId),
                ("Group",           "Student")              => await context.Students.CountAsync(x => x.GroupId == parentId),
                ("Group",           "SubjectOffering")      => await context.SubjectOfferings.CountAsync(x => x.GroupId == parentId),
                ("Group",           "ScheduleEntry")        => await context.ScheduleEntries.CountAsync(x => x.GroupId == parentId),
                ("Subject",         "SubjectOffering")      => await context.SubjectOfferings.CountAsync(x => x.SubjectId == parentId),
                ("Subject",         "SubjectDoctor")        => await context.SubjectDoctors.CountAsync(x => x.SubjectId == parentId),
                ("Subject",         "SubjectAssistant")     => await context.SubjectAssistants.CountAsync(x => x.SubjectId == parentId),
                ("Subject",         "RegulationSubject")    => await context.RegulationSubjects.CountAsync(x => x.SubjectId == parentId),
                ("Subject",         "AttendanceSession")    => await context.AttendanceSessions.CountAsync(x => x.SubjectId == parentId),
                ("Regulation",      "RegulationSubject")    => await context.RegulationSubjects.CountAsync(x => x.RegulationId == parentId),
                ("Semester",        "SubjectOffering")      => await context.SubjectOfferings.CountAsync(x => x.SemesterId == parentId),
                ("SubjectOffering", "Enrollment")           => await context.Enrollments.CountAsync(x => x.SubjectOfferingId == parentId),
                ("SubjectOffering", "Exam")                 => await context.Exams.CountAsync(x => x.SubjectOfferingId == parentId),
                ("SubjectOffering", "StudentGrade")         => await context.StudentGrades.CountAsync(x => x.SubjectOfferingId == parentId),
                ("SubjectOffering", "Material")             => await context.Materials.CountAsync(x => x.SubjectOfferingId == parentId),
                ("SubjectOffering", "ScheduleEntry")        => await context.ScheduleEntries.CountAsync(x => x.SubjectOfferingId == parentId),
                ("Doctor",          "SubjectOffering")      => await context.SubjectOfferings.CountAsync(x => x.DoctorId == parentId),
                ("Doctor",          "SubjectDoctor")        => await context.SubjectDoctors.CountAsync(x => x.DoctorId == parentId),
                ("Doctor",          "AttendanceSession")    => await context.AttendanceSessions.CountAsync(x => x.DoctorId == parentId),
                ("Doctor",          "Material")             => await context.Materials.CountAsync(x => x.UploadedByDoctorId == parentId),
                ("Doctor",          "Exam")                 => await context.Exams.CountAsync(x => x.CreatedByDoctorId == parentId),
                ("TeachingAssistant", "SubjectAssistant")   => await context.SubjectAssistants.CountAsync(x => x.TeachingAssistantId == parentId),
                ("TeachingAssistant", "AttendanceSession")  => await context.AttendanceSessions.CountAsync(x => x.TeachingAssistantId == parentId),
                ("Student",         "Enrollment")           => await context.Enrollments.CountAsync(x => x.StudentId == parentId),
                ("Student",         "StudentGrade")         => await context.StudentGrades.CountAsync(x => x.StudentId == parentId),
                ("Student",         "ExamSubmission")       => await context.ExamSubmissions.CountAsync(x => x.StudentId == parentId),
                ("Student",         "StudentAttendance")    => await context.StudentAttendances.CountAsync(x => x.StudentId == parentId),
                ("Student",         "StudentExamVariant")   => await context.StudentExamVariants.CountAsync(x => x.StudentId == parentId),
                ("Student",         "StudentFile")          => await context.StudentFiles.CountAsync(x => x.UploadedByStudentId == parentId),
                ("Student",         "Complaint")            => await context.Complaints.CountAsync(x => x.StudentId == parentId),
                ("SystemUser",      "Conversation")         => await context.Conversations.CountAsync(x => x.UserId == parentId),
                ("SystemUser",      "AiMemory")             => await context.AiMemories.CountAsync(x => x.UserId == parentId),
                ("SystemUser",      "AppNotification")      => await context.AppNotifications.CountAsync(x => x.UserId == parentId),
                ("SystemUser",      "RefreshToken")         => await context.RefreshTokens.CountAsync(x => x.UserId == parentId),
                ("Exam",            "ExamQuestion")         => await context.ExamQuestions.CountAsync(x => x.ExamId == parentId),
                ("Exam",            "ExamSubmission")       => await context.ExamSubmissions.CountAsync(x => x.ExamId == parentId),
                ("Exam",            "StudentExamVariant")   => await context.StudentExamVariants.CountAsync(x => x.ExamId == parentId),
                ("Complaint",       "ComplaintAnalysis")    => await context.ComplaintAnalyses.CountAsync(x => x.ComplaintId == parentId),
                ("Conversation",    "ChatMessage")          => await context.ChatMessages.CountAsync(x => x.ConversationId == parentId),
                ("AttendanceSession","StudentAttendance")   => await context.StudentAttendances.CountAsync(x => x.AttendanceSessionId == parentId),
                _                                           => 0
            };
        }

        private async Task<Ulid?> GetFirstChildIdAsync(string parentEntity, Ulid parentId, string childEntity)
        {
            return (parentEntity, childEntity) switch
            {
                ("College",      "Department")      => (await context.Departments.FirstOrDefaultAsync(x => x.CollegeId == parentId))?.Id,
                ("Department",   "Batch")           => (await context.Batches.FirstOrDefaultAsync(x => x.DepartmentId == parentId))?.Id,
                ("Batch",        "Group")           => (await context.Groups.FirstOrDefaultAsync(x => x.BatchId == parentId))?.Id,
                ("Group",        "Student")         => (await context.Students.FirstOrDefaultAsync(x => x.GroupId == parentId))?.Id,
                ("AcademicYear", "Semester")        => (await context.Semesters.FirstOrDefaultAsync(x => x.AcademicYearId == parentId))?.Id,
                ("Semester",     "SubjectOffering") => (await context.SubjectOfferings.FirstOrDefaultAsync(x => x.SemesterId == parentId))?.Id,
                ("SubjectOffering","Exam")          => (await context.Exams.FirstOrDefaultAsync(x => x.SubjectOfferingId == parentId))?.Id,
                _                                   => null
            };
        }

        // ── Display name resolver ────────────────────────────────────────────

        private async Task<string> GetDisplayNameAsync(string entityName, Ulid entityId)
        {
            return entityName switch
            {
                "University"      => (await context.Universities.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "College"         => (await context.Colleges.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Department"      => (await context.Departments.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "AcademicYear"    => (await context.AcademicYears.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Semester"        => (await context.Semesters.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Batch"           => (await context.Batches.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Group"           => (await context.Groups.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Subject"         => (await context.Subjects.FindAsync(entityId))?.Name ?? entityId.ToString(),
                "Regulation"      => (await context.Regulations.FindAsync(entityId))?.Title ?? entityId.ToString(),
                "Student"         => (await context.Students.FindAsync(entityId))?.FullName ?? entityId.ToString(),
                "Doctor"          => (await context.Doctors.FindAsync(entityId))?.FullName ?? entityId.ToString(),
                "TeachingAssistant" => (await context.TeachingAssistants.FindAsync(entityId))?.FullName ?? entityId.ToString(),
                "Exam"            => (await context.Exams.FindAsync(entityId))?.Title ?? entityId.ToString(),
                _                 => entityId.ToString()
            };
        }

        // ── Confirmation requirements builder ────────────────────────────────

        private static ConfirmationRequirementsDto BuildConfirmationRequirements(
            EntityDeletionPolicy policy,
            string displayName)
        {
            var phrase = policy.ConfirmationPhrase != null
                ? $"{policy.ConfirmationPhrase}: {displayName}".ToUpperInvariant()
                : null;

            return policy.RiskLevel switch
            {
                DeleteRiskLevel.Low => new()
                {
                    RequiresTypedConfirmation    = false,
                    RequiresPasswordConfirmation = false,
                    RequiresSecondAdminApproval  = false,
                    ConfirmationSteps            = 1
                },
                DeleteRiskLevel.Medium => new()
                {
                    RequiresTypedConfirmation    = false,
                    RequiresPasswordConfirmation = false,
                    RequiresSecondAdminApproval  = false,
                    ConfirmationSteps            = 1,
                    TypedConfirmationPhrase      = string.Empty
                },
                DeleteRiskLevel.High => new()
                {
                    RequiresTypedConfirmation    = true,
                    TypedConfirmationPhrase      = phrase ?? displayName.ToUpperInvariant(),
                    RequiresPasswordConfirmation = false,
                    RequiresSecondAdminApproval  = false,
                    ConfirmationSteps            = 2
                },
                DeleteRiskLevel.Critical => new()
                {
                    RequiresTypedConfirmation    = true,
                    TypedConfirmationPhrase      = phrase ?? displayName.ToUpperInvariant(),
                    RequiresPasswordConfirmation = true,
                    RequiresSecondAdminApproval  = false,
                    ConfirmationSteps            = 3
                },
                DeleteRiskLevel.Catastrophic => new()
                {
                    RequiresTypedConfirmation    = true,
                    TypedConfirmationPhrase      = phrase ?? displayName.ToUpperInvariant(),
                    RequiresPasswordConfirmation = true,
                    RequiresSecondAdminApproval  = true,
                    ConfirmationSteps            = 4
                },
                _ => new() { ConfirmationSteps = 1 }
            };
        }

        // ── Warning builder ───────────────────────────────────────────────────

        private static void BuildWarnings(
            string entityName,
            EntityDeletionPolicy policy,
            Dictionary<string, int> summary,
            List<DeleteBlockerDto> blockers,
            List<string> warnings)
        {
            if (policy.IsHistorical)
                warnings.Add("This operation will affect historical academic data that cannot be recovered.");

            if (policy.DeleteType == DeleteType.SoftDelete)
                warnings.Add("This record will be soft-deleted (deactivated). It will not appear in normal queries but the data is preserved.");
            else if (policy.DeleteType == DeleteType.HardDelete)
                warnings.Add("This record will be permanently and irreversibly deleted from the database.");
            else if (policy.DeleteType == DeleteType.ImmutableBlocked)
                warnings.Add("This record is immutable and cannot be deleted under any circumstances.");
            else if (policy.DeleteType == DeleteType.ArchiveOnly)
                warnings.Add("This record can only be archived, not deleted.");

            if (summary.TryGetValue("StudentGrade", out var gradeCount) && gradeCount > 0)
                warnings.Add($"⚠️ {gradeCount} student grade records will be affected — GPA calculations and transcripts may be impacted.");

            if (summary.TryGetValue("Enrollment", out var enrCount) && enrCount > 0)
                warnings.Add($"⚠️ {enrCount} enrollment records represent active academic contracts.");

            if (summary.TryGetValue("ExamSubmission", out var subCount) && subCount > 0)
                warnings.Add($"⚠️ {subCount} exam submissions are legal academic records.");

            if (summary.TryGetValue("Student", out var stuCount) && stuCount > 0)
                warnings.Add($"⚠️ {stuCount} students will lose access to the system and their academic history.");

            if (summary.ContainsKey("AiMemory") || summary.ContainsKey("Conversation"))
                warnings.Add("AI conversation history and personalized memory data will be removed.");

            if (blockers.Count > 0)
                warnings.Add("🚫 This operation is currently BLOCKED. Resolve all blockers before proceeding.");

            if (policy.RiskLevel == DeleteRiskLevel.Catastrophic)
            {
                warnings.Add("☠️ CATASTROPHIC RISK: This is an irreversible operation with institution-wide impact.");
                warnings.Add("This operation requires second admin approval and cannot be undone.");
            }
        }

        // ── Deletion order builder ────────────────────────────────────────────

        private static List<string> BuildDeletionOrder(
            string rootEntity,
            Dictionary<string, int> summary)
        {
            var involvedEntities = new HashSet<string>(summary.Keys) { rootEntity };
            return DependencyGraph.SafeDeletionOrder
                .Where(e => involvedEntities.Contains(e))
                .ToList();
        }

        // ── Execution router ──────────────────────────────────────────────────

        private async Task ExecuteDeleteAsync(
            string entityName,
            Ulid entityId,
            EntityDeletionPolicy policy,
            List<string> executedSteps,
            Dictionary<string, int> affectedCounts)
        {
            switch (policy.DeleteType)
            {
                case DeleteType.SoftDelete:
                    await SoftDeleteEntityAsync(entityName, entityId, executedSteps, affectedCounts);
                    break;
                case DeleteType.HardDelete:
                    await HardDeleteEntityAsync(entityName, entityId, executedSteps, affectedCounts);
                    break;
                case DeleteType.ImmutableBlocked:
                    throw new InvalidOperationException($"{policy.FriendlyName} is immutable and cannot be deleted.");
                case DeleteType.Restricted:
                    throw new InvalidOperationException($"{policy.FriendlyName} is restricted from deletion.");
                case DeleteType.ArchiveOnly:
                    throw new InvalidOperationException($"{policy.FriendlyName} can only be archived, not deleted.");
            }
        }

        private async Task SoftDeleteEntityAsync(
            string entityName,
            Ulid entityId,
            List<string> steps,
            Dictionary<string, int> counts)
        {
            var now = DateTime.UtcNow;

            switch (entityName)
            {
                case "University":
                    var uni = await context.Universities.FindAsync(entityId);
                    if (uni != null) { uni.DeletedAt = now; counts["University"] = 1; steps.Add("Soft-deleted University"); }
                    break;
                case "College":
                    var col = await context.Colleges.FindAsync(entityId);
                    if (col != null) { col.DeletedAt = now; counts["College"] = 1; steps.Add("Soft-deleted College"); }
                    break;
                case "Department":
                    var dept = await context.Departments.FindAsync(entityId);
                    if (dept != null) { dept.DeletedAt = now; counts["Department"] = 1; steps.Add("Soft-deleted Department"); }
                    break;
                case "AcademicYear":
                    var ay = await context.AcademicYears.FindAsync(entityId);
                    if (ay != null) { ay.DeletedAt = now; counts["AcademicYear"] = 1; steps.Add("Soft-deleted Academic Year"); }
                    break;
                case "Semester":
                    var sem = await context.Semesters.FindAsync(entityId);
                    if (sem != null) { sem.DeletedAt = now; counts["Semester"] = 1; steps.Add("Soft-deleted Semester"); }
                    break;
                case "Batch":
                    var batch = await context.Batches.FindAsync(entityId);
                    if (batch != null) { batch.DeletedAt = now; counts["Batch"] = 1; steps.Add("Soft-deleted Batch"); }
                    break;
                case "Group":
                    var grp = await context.Groups.FindAsync(entityId);
                    if (grp != null) { grp.DeletedAt = now; counts["Group"] = 1; steps.Add("Soft-deleted Group"); }
                    break;
                case "Subject":
                    var subj = await context.Subjects.FindAsync(entityId);
                    if (subj != null) { subj.DeletedAt = now; counts["Subject"] = 1; steps.Add("Soft-deleted Subject"); }
                    break;
                case "Regulation":
                    var reg = await context.Regulations.FindAsync(entityId);
                    if (reg != null) { reg.DeletedAt = now; counts["Regulation"] = 1; steps.Add("Soft-deleted Regulation"); }
                    break;
                case "SubjectOffering":
                    var so = await context.SubjectOfferings.FindAsync(entityId);
                    if (so != null) { so.DeletedAt = now; counts["SubjectOffering"] = 1; steps.Add("Soft-deleted Subject Offering"); }
                    break;
                case "Student":
                    var stu = await context.Students.FindAsync(entityId);
                    if (stu != null)
                    {
                        stu.DeletedAt = now;
                        stu.IsActive  = false;
                        counts["Student"] = 1;
                        steps.Add("Soft-deleted Student and set IsActive=false");
                        // Also soft-delete linked SystemUser
                        var usr = await context.SystemUsers.FindAsync(stu.SystemUserId);
                        if (usr != null) { usr.DeletedAt = now; counts["SystemUser"] = 1; steps.Add("Soft-deleted linked SystemUser"); }
                    }
                    break;
                case "Doctor":
                    var doc = await context.Doctors.FindAsync(entityId);
                    if (doc != null)
                    {
                        doc.DeletedAt = now;
                        counts["Doctor"] = 1;
                        steps.Add("Soft-deleted Doctor");
                        var docUsr = await context.SystemUsers.FindAsync(doc.SystemUserId);
                        if (docUsr != null) { docUsr.DeletedAt = now; counts["SystemUser"] = 1; steps.Add("Soft-deleted linked SystemUser"); }
                    }
                    break;
                case "TeachingAssistant":
                    var ta = await context.TeachingAssistants.FindAsync(entityId);
                    if (ta != null)
                    {
                        ta.DeletedAt = now;
                        counts["TeachingAssistant"] = 1;
                        steps.Add("Soft-deleted Teaching Assistant");
                        var taUsr = await context.SystemUsers.FindAsync(ta.SystemUserId);
                        if (taUsr != null) { taUsr.DeletedAt = now; counts["SystemUser"] = 1; steps.Add("Soft-deleted linked SystemUser"); }
                    }
                    break;
                case "Exam":
                    var exam = await context.Exams.FindAsync(entityId);
                    if (exam != null) { exam.DeletedAt = now; counts["Exam"] = 1; steps.Add("Soft-deleted Exam"); }
                    break;
                case "AttendanceSession":
                    var atSess = await context.AttendanceSessions.FindAsync(entityId);
                    if (atSess != null) { atSess.DeletedAt = now; counts["AttendanceSession"] = 1; steps.Add("Soft-deleted Attendance Session"); }
                    break;
                case "StudentAttendance":
                    var atRec = await context.StudentAttendances.FindAsync(entityId);
                    if (atRec != null) { atRec.DeletedAt = now; counts["StudentAttendance"] = 1; steps.Add("Soft-deleted Attendance Record"); }
                    break;
                case "Material":
                    var mat = await context.Materials.FindAsync(entityId);
                    if (mat != null) { mat.DeletedAt = now; counts["Material"] = 1; steps.Add("Soft-deleted Material"); }
                    break;
                case "UploadedFile":
                    var uf = await context.UploadedFiles.FindAsync(entityId);
                    if (uf != null) { uf.DeletedAt = now; counts["UploadedFile"] = 1; steps.Add("Soft-deleted Uploaded File"); }
                    break;
                case "Conversation":
                    var conv = await context.Conversations.FindAsync(entityId);
                    if (conv != null) { conv.DeletedAt = now; counts["Conversation"] = 1; steps.Add("Soft-deleted Conversation"); }
                    break;
                case "Complaint":
                    var comp = await context.Complaints.FindAsync(entityId);
                    if (comp != null) { comp.DeletedAt = now; counts["Complaint"] = 1; steps.Add("Soft-deleted Complaint"); }
                    break;
                default:
                    steps.Add($"[WARN] No soft-delete handler for {entityName} — skipped");
                    break;
            }
        }

        private async Task HardDeleteEntityAsync(
            string entityName,
            Ulid entityId,
            List<string> steps,
            Dictionary<string, int> counts)
        {
            switch (entityName)
            {
                case "AppNotification":
                    var notif = await context.AppNotifications.FindAsync(entityId);
                    if (notif != null) { context.AppNotifications.Remove(notif); counts["AppNotification"] = 1; steps.Add("Hard-deleted Notification"); }
                    break;
                case "RefreshToken":
                    var rt = await context.RefreshTokens.FindAsync(entityId);
                    if (rt != null) { context.RefreshTokens.Remove(rt); counts["RefreshToken"] = 1; steps.Add("Hard-deleted Refresh Token"); }
                    break;
                case "AiMemory":
                    var mem = await context.AiMemories.FindAsync(entityId);
                    if (mem != null) { context.AiMemories.Remove(mem); counts["AiMemory"] = 1; steps.Add("Hard-deleted AI Memory"); }
                    break;
                case "ComplaintAnalysis":
                    var ca = await context.ComplaintAnalyses.FindAsync(entityId);
                    if (ca != null) { context.ComplaintAnalyses.Remove(ca); counts["ComplaintAnalysis"] = 1; steps.Add("Hard-deleted Complaint Analysis"); }
                    break;
                case "ComplaintCluster":
                    var cc = await context.ComplaintClusters.FindAsync(entityId);
                    if (cc != null) { context.ComplaintClusters.Remove(cc); counts["ComplaintCluster"] = 1; steps.Add("Hard-deleted Complaint Cluster"); }
                    break;
                case "ChatMessage":
                    var msg = await context.ChatMessages.FindAsync(entityId);
                    if (msg != null) { context.ChatMessages.Remove(msg); counts["ChatMessage"] = 1; steps.Add("Hard-deleted Chat Message"); }
                    break;
                case "ExamQuestion":
                    var eq = await context.ExamQuestions.FindAsync(entityId);
                    if (eq != null) { context.ExamQuestions.Remove(eq); counts["ExamQuestion"] = 1; steps.Add("Hard-deleted Exam Question"); }
                    break;
                case "StudentExamVariant":
                    var ev = await context.StudentExamVariants.FindAsync(entityId);
                    if (ev != null) { context.StudentExamVariants.Remove(ev); counts["StudentExamVariant"] = 1; steps.Add("Hard-deleted Exam Variant"); }
                    break;
                case "StudentFile":
                    var sf = await context.StudentFiles.FindAsync(entityId);
                    if (sf != null) { context.StudentFiles.Remove(sf); counts["StudentFile"] = 1; steps.Add("Hard-deleted Student File"); }
                    break;
                case "ScheduleEntry":
                    var se = await context.ScheduleEntries.FindAsync(entityId);
                    if (se != null) { context.ScheduleEntries.Remove(se); counts["ScheduleEntry"] = 1; steps.Add("Hard-deleted Schedule Entry"); }
                    break;
                default:
                    steps.Add($"[WARN] No hard-delete handler for {entityName} — skipped");
                    break;
            }
        }

        // ── Snapshot for audit ────────────────────────────────────────────────

        private async Task<string?> GetEntitySnapshotAsync(string entityName, Ulid entityId)
        {
            try
            {
                object? entity = entityName switch
                {
                    "University"      => await context.Universities.FindAsync(entityId),
                    "College"         => await context.Colleges.FindAsync(entityId),
                    "Department"      => await context.Departments.FindAsync(entityId),
                    "AcademicYear"    => await context.AcademicYears.FindAsync(entityId),
                    "Semester"        => await context.Semesters.FindAsync(entityId),
                    "Batch"           => await context.Batches.FindAsync(entityId),
                    "Group"           => await context.Groups.FindAsync(entityId),
                    "Subject"         => await context.Subjects.FindAsync(entityId),
                    "Student"         => await context.Students.FindAsync(entityId),
                    "Doctor"          => await context.Doctors.FindAsync(entityId),
                    "Exam"            => await context.Exams.FindAsync(entityId),
                    "Regulation"      => await context.Regulations.FindAsync(entityId),
                    _                 => null
                };
                return entity == null ? null : JsonSerializer.Serialize(entity,
                    _snapshotOptions);
            }
            catch { return null; }
        }
    }
}
