using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Exceptions;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Handles soft-delete and hard-delete cascade logic for the academic hierarchy.
    ///
    /// EXTRACTED FROM AppDbContext — business logic does not belong in DbContext.
    /// AppDbContext.CascadeDeleteAsync / HardCascadeDeleteAsync now delegate here.
    ///
    /// Academic Hierarchy:
    ///   University → College → Department → Batch → Group → SubjectOffering
    ///
    /// Soft Delete:  sets DeletedAt, preserves data, cascades to children
    /// Hard Delete:  permanent SQL DELETE, bottom-up, blocks if users are linked
    /// </summary>
    public class CascadeDeleteService(AppDbContext db)
    {
        // ── Soft Cascade Delete ───────────────────────────────────────────────

        public async Task SoftCascadeAsync(BaseEntity entity)
        {
            var now = DateTime.UtcNow;
            await SoftCascadeChildrenAsync(entity, now);
            entity.DeletedAt = now;
            db.Entry(entity).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        private async Task SoftCascadeChildrenAsync(BaseEntity entity, DateTime now)
        {
            switch (entity)
            {
                case University u:
                {
                    var children = await db.Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id && c.DeletedAt == null).ToListAsync();
                    foreach (var child in children)
                    { await SoftCascadeChildrenAsync(child, now); child.DeletedAt = now; }
                    break;
                }
                case College c:
                {
                    var children = await db.Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == c.Id && d.DeletedAt == null).ToListAsync();
                    foreach (var child in children)
                    { await SoftCascadeChildrenAsync(child, now); child.DeletedAt = now; }
                    break;
                }
                case Department dept:
                {
                    var batches = await db.Batches.IgnoreQueryFilters()
                        .Where(b => b.DepartmentId == dept.Id && b.DeletedAt == null).ToListAsync();
                    foreach (var b in batches)
                    { await SoftCascadeChildrenAsync(b, now); b.DeletedAt = now; }
                    break;
                }
                case Batch batch:
                {
                    var groups = await db.Groups.IgnoreQueryFilters()
                        .Where(g => g.BatchId == batch.Id && g.DeletedAt == null).ToListAsync();
                    foreach (var g in groups)
                    { await SoftCascadeChildrenAsync(g, now); g.DeletedAt = now; }

                    var offerings = await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.DeletedAt == null).ToListAsync();
                    foreach (var so in offerings)
                    { await SoftCascadeChildrenAsync(so, now); so.DeletedAt = now; }
                    break;
                }
                case Group group:
                {
                    var offerings = await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id && so.DeletedAt == null).ToListAsync();
                    foreach (var so in offerings)
                    { await SoftCascadeChildrenAsync(so, now); so.DeletedAt = now; }
                    break;
                }
                case SubjectOffering offering:
                {
                    var now2 = now;
                    (await db.Enrollments.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id && e.DeletedAt == null).ToListAsync())
                        .ForEach(e => e.DeletedAt = now2);

                    (await db.Materials.IgnoreQueryFilters()
                        .Where(m => m.SubjectOfferingId == offering.Id && m.DeletedAt == null).ToListAsync())
                        .ForEach(m => m.DeletedAt = now2);

                    (await db.StudentGrades.IgnoreQueryFilters()
                        .Where(g => g.SubjectOfferingId == offering.Id && g.DeletedAt == null).ToListAsync())
                        .ForEach(g => g.DeletedAt = now2);

                    (await db.UploadedFiles.IgnoreQueryFilters()
                        .Where(f => f.SubjectOfferingId == offering.Id && f.DeletedAt == null).ToListAsync())
                        .ForEach(f => f.DeletedAt = now2);

                    var exams = await db.Exams.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id && e.DeletedAt == null).ToListAsync();
                    foreach (var e in exams)
                    { await SoftCascadeChildrenAsync(e, now); e.DeletedAt = now; }
                    break;
                }
                case Exam exam:
                {
                    (await db.ExamSubmissions.IgnoreQueryFilters()
                        .Where(s => s.ExamId == exam.Id && s.DeletedAt == null).ToListAsync())
                        .ForEach(s => s.DeletedAt = now);
                    break;
                }
            }
        }

        // ── Hard Cascade Delete ───────────────────────────────────────────────

        public async Task HardCascadeAsync(BaseEntity entity)
        {
            await EnsureNoLinkedUsersAsync(entity);
            await HardCascadeChildrenAsync(entity);

            switch (entity)
            {
                case University u:
                    await db.Universities.IgnoreQueryFilters().Where(x => x.Id == u.Id).ExecuteDeleteAsync(); break;
                case College c:
                    await db.Colleges.IgnoreQueryFilters().Where(x => x.Id == c.Id).ExecuteDeleteAsync(); break;
                case Department d:
                    await db.Departments.IgnoreQueryFilters().Where(x => x.Id == d.Id).ExecuteDeleteAsync(); break;
                case Batch b:
                    await db.Batches.IgnoreQueryFilters().Where(x => x.Id == b.Id).ExecuteDeleteAsync(); break;
                case Group g:
                    await db.Groups.IgnoreQueryFilters().Where(x => x.Id == g.Id).ExecuteDeleteAsync(); break;
                case SubjectOffering so:
                    await db.SubjectOfferings.IgnoreQueryFilters().Where(x => x.Id == so.Id).ExecuteDeleteAsync(); break;
            }
        }

        private async Task EnsureNoLinkedUsersAsync(BaseEntity entity)
        {
            switch (entity)
            {
                case Group group:
                {
                    var count = await db.Students.IgnoreQueryFilters().CountAsync(s => s.GroupId == group.Id);
                    if (count > 0) throw new DomainException(
                        $"Cannot delete Group: {count} student(s) assigned. Reassign first.");
                    break;
                }
                case Batch batch:
                {
                    var count = await db.Students.IgnoreQueryFilters().CountAsync(s => s.BatchId == batch.Id);
                    if (count > 0) throw new DomainException(
                        $"Cannot delete Batch: {count} student(s) belong to it. Reassign first.");
                    break;
                }
                case Department dept:
                {
                    var s = await db.Students.IgnoreQueryFilters().CountAsync(x => x.DepartmentId == dept.Id);
                    var d = await db.Doctors.IgnoreQueryFilters().CountAsync(x => x.DepartmentId == dept.Id);
                    var t = await db.TeachingAssistants.IgnoreQueryFilters().CountAsync(x => x.DepartmentId == dept.Id);
                    if (s + d + t > 0) throw new DomainException(
                        $"Cannot delete Department: {s} student(s), {d} doctor(s), {t} TA(s) linked. Reassign first.");
                    break;
                }
                case College college:
                {
                    var depts = await db.Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == college.Id).ToListAsync();
                    foreach (var dep in depts) await EnsureNoLinkedUsersAsync(dep);
                    break;
                }
                case University u:
                {
                    var colleges = await db.Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id).ToListAsync();
                    foreach (var col in colleges) await EnsureNoLinkedUsersAsync(col);
                    break;
                }
            }
        }

        private async Task HardCascadeChildrenAsync(BaseEntity entity)
        {
            switch (entity)
            {
                case University u:
                {
                    var colleges = await db.Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id).ToListAsync();
                    foreach (var c in colleges) await HardCascadeChildrenAsync(c);
                    await db.Colleges.IgnoreQueryFilters().Where(c => c.UniversityId == u.Id).ExecuteDeleteAsync();
                    break;
                }
                case College college:
                {
                    var depts = await db.Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == college.Id).ToListAsync();
                    foreach (var d in depts) await HardCascadeChildrenAsync(d);
                    await db.Departments.IgnoreQueryFilters().Where(d => d.CollegeId == college.Id).ExecuteDeleteAsync();

                    var yearIds = await db.Set<AcademicYear>().IgnoreQueryFilters()
                        .Where(y => y.CollegeId == college.Id).Select(y => y.Id).ToListAsync();
                    if (yearIds.Count > 0)
                    {
                        await db.Set<Semester>().IgnoreQueryFilters()
                            .Where(s => yearIds.Contains(s.AcademicYearId)).ExecuteDeleteAsync();
                        await db.Set<AcademicYear>().IgnoreQueryFilters()
                            .Where(y => y.CollegeId == college.Id).ExecuteDeleteAsync();
                    }
                    break;
                }
                case Department dept:
                {
                    await db.AcademicYearDepartments.Where(m => m.DepartmentId == dept.Id).ExecuteDeleteAsync();

                    var regIds = await db.Regulations.IgnoreQueryFilters()
                        .Where(r => r.DepartmentId == dept.Id).Select(r => r.Id).ToListAsync();
                    if (regIds.Count > 0)
                    {
                        await db.RegulationSubjects.IgnoreQueryFilters()
                            .Where(rs => regIds.Contains(rs.RegulationId)).ExecuteDeleteAsync();
                        await db.Regulations.IgnoreQueryFilters()
                            .Where(r => r.DepartmentId == dept.Id).ExecuteDeleteAsync();
                    }

                    var batches = await db.Batches.IgnoreQueryFilters()
                        .Where(b => b.DepartmentId == dept.Id).ToListAsync();
                    foreach (var b in batches) await HardCascadeChildrenAsync(b);
                    await db.Batches.IgnoreQueryFilters().Where(b => b.DepartmentId == dept.Id).ExecuteDeleteAsync();
                    break;
                }
                case Batch batch:
                {
                    var groups = await db.Groups.IgnoreQueryFilters()
                        .Where(g => g.BatchId == batch.Id).ToListAsync();
                    foreach (var g in groups) await HardCascadeChildrenAsync(g);
                    await db.Groups.IgnoreQueryFilters().Where(g => g.BatchId == batch.Id).ExecuteDeleteAsync();

                    var offerings = await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.GroupId == null).ToListAsync();
                    foreach (var so in offerings) await HardCascadeChildrenAsync(so);
                    await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.GroupId == null).ExecuteDeleteAsync();
                    break;
                }
                case Group group:
                {
                    var offerings = await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id).ToListAsync();
                    foreach (var so in offerings) await HardCascadeChildrenAsync(so);
                    await db.SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id).ExecuteDeleteAsync();
                    break;
                }
                case SubjectOffering offering:
                {
                    var examIds = await db.Exams.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id).Select(e => e.Id).ToListAsync();
                    if (examIds.Count > 0)
                    {
                        await db.ExamSubmissions.IgnoreQueryFilters()
                            .Where(s => examIds.Contains(s.ExamId)).ExecuteDeleteAsync();
                        await db.Exams.IgnoreQueryFilters()
                            .Where(e => e.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    }
                    await db.Enrollments.IgnoreQueryFilters().Where(e => e.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await db.Materials.IgnoreQueryFilters().Where(m => m.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await db.StudentGrades.IgnoreQueryFilters().Where(g => g.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await db.UploadedFiles.IgnoreQueryFilters().Where(f => f.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    break;
                }
            }
        }
    }
}
