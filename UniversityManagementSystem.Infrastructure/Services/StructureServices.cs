using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class UniversityService(IGenericRepository<University> repo, AppDbContext db) : IUniversityService
    {
        private readonly IGenericRepository<University> _repo = repo;
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<University>> GetAllUniversitiesAsync() => await _repo.GetAllAsync();
        public async Task<University?> GetUniversityByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<University?> GetUniversityByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToUpper();
            var results = await _repo.GetAsync(u => u.Code.ToUpper() == normalizedCode);
            return results.FirstOrDefault();
        }

        public async Task<University> CreateUniversityAsync(University university) => await _repo.AddAsync(university);
        public async Task UpdateUniversityAsync(University university) => await _repo.UpdateAsync(university);
        public async Task DeleteUniversityAsync(Ulid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _db.CascadeDeleteAsync(entity);
        }
    }

    public class CollegeService(IGenericRepository<College> repo, ISmartStringGenerator smartString, AppDbContext db) : ICollegeService
    {
        private readonly IGenericRepository<College> _repo = repo;
        private readonly ISmartStringGenerator _smartString = smartString;
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<College>> GetAllCollegesAsync() => await _repo.GetAllAsync();
        public async Task<IReadOnlyList<College>> GetCollegesByUniversityIdAsync(Ulid universityId)
            => await _repo.GetAsync(c => c.UniversityId == universityId);

        public async Task<College?> GetCollegeByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<College?> GetCollegeByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            var colleges = await _repo.GetAsync(c => c.Code.ToLower() == normalizedCode);
            return colleges.FirstOrDefault();
        }
        public async Task<College> CreateCollegeAsync(College college)
        {
            college.Name = await _smartString.GenerateUniqueAsync<College>(college.Name, c => c.Name);
            return await _repo.AddAsync(college);
        }
        public async Task UpdateCollegeAsync(College college) => await _repo.UpdateAsync(college);
        public async Task DeleteCollegeAsync(Ulid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _db.CascadeDeleteAsync(entity);
        }
    }

    public class DepartmentService(IGenericRepository<Department> repo, ISmartStringGenerator smartString, AppDbContext db) : IDepartmentService
    {
        private readonly IGenericRepository<Department> _repo = repo;
        private readonly ISmartStringGenerator _smartString = smartString;
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<Department>> GetDepartmentsByCollegeIdAsync(Ulid collegeId)
            => await _repo.GetAsync(d => d.CollegeId == collegeId);

        public async Task<Department?> GetDepartmentByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<Department?> GetDepartmentByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            var departments = await _repo.GetAsync(d => d.Code.ToLower() == normalizedCode);
            return departments.FirstOrDefault();
        }
        public async Task<Department> CreateDepartmentAsync(Department department)
        {
            department.Name = await _smartString.GenerateUniqueAsync<Department>(department.Name, d => d.Name);
            return await _repo.AddAsync(department);
        }
        public async Task UpdateDepartmentAsync(Department department) => await _repo.UpdateAsync(department);
        public async Task DeleteDepartmentAsync(Ulid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _db.CascadeDeleteAsync(entity);
        }
    }

    public class BatchService(IGenericRepository<Batch> repo, AppDbContext db) : IBatchService
    {
        private readonly IGenericRepository<Batch> _repo = repo;
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<Batch>> GetBatchesByDepartmentIdAsync(Ulid departmentId)
            => await _repo.GetAsync(b => b.DepartmentId == departmentId);

        public async Task<Batch?> GetBatchByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);
        public async Task<Batch?> GetBatchByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            var batches = await _repo.GetAsync(b => b.Code.ToLower() == normalizedCode);
            return batches.FirstOrDefault();
        }
        public async Task<Batch> CreateBatchAsync(Batch batch) => await _repo.AddAsync(batch);
        public async Task UpdateBatchAsync(Batch batch) => await _repo.UpdateAsync(batch);
        public async Task DeleteBatchAsync(Ulid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _db.CascadeDeleteAsync(entity);
        }
    }

    public class GroupService(IGenericRepository<Group> repo, AppDbContext db) : IGroupService
    {
        private readonly IGenericRepository<Group> _repo = repo;
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<Group>> GetGroupsByBatchIdAsync(Ulid batchId)
            => await _repo.GetAsync(g => g.BatchId == batchId);

        public async Task<Group?> GetGroupByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);
        public async Task<Group?> GetGroupByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            var groups = await _repo.GetAsync(g => g.Code.ToLower() == normalizedCode);
            return groups.FirstOrDefault();
        }
        public async Task<Group> CreateGroupAsync(Group group) => await _repo.AddAsync(group);
        public async Task UpdateGroupAsync(Group group) => await _repo.UpdateAsync(group);
        public async Task DeleteGroupAsync(Ulid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _db.CascadeDeleteAsync(entity);
        }
    }
}
