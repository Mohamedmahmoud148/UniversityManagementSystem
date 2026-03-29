using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class UniversityService(IGenericRepository<University> repo, IGenericRepository<College> collegeRepo) : IUniversityService
    {
        private readonly IGenericRepository<University> _repo = repo;
        private readonly IGenericRepository<College> _collegeRepo = collegeRepo;

        public async Task<IReadOnlyList<University>> GetAllUniversitiesAsync() => await _repo.GetAllAsync();
        public async Task<University?> GetUniversityByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);
        public async Task<University> CreateUniversityAsync(University university) => await _repo.AddAsync(university);
        public async Task UpdateUniversityAsync(University university) => await _repo.UpdateAsync(university);
        public async Task DeleteUniversityAsync(Ulid id)
        {
            var hasChildren = (await _collegeRepo.GetAsync(c => c.UniversityId == id)).Any();
            if (hasChildren) throw new InvalidOperationException("Cannot delete University with associated Colleges.");

            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _repo.DeleteAsync(entity);
        }
    }

    public class CollegeService(IGenericRepository<College> repo, IGenericRepository<Department> deptRepo, ISmartStringGenerator smartString) : ICollegeService
    {
        private readonly IGenericRepository<College> _repo = repo;
        private readonly IGenericRepository<Department> _deptRepo = deptRepo;
        private readonly ISmartStringGenerator _smartString = smartString;

        public async Task<IReadOnlyList<College>> GetAllCollegesAsync() => await _repo.GetAllAsync();
        public async Task<IReadOnlyList<College>> GetCollegesByUniversityIdAsync(Ulid universityId)
            => await _repo.GetAsync(c => c.UniversityId == universityId);

        public async Task<College?> GetCollegeByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<College?> GetCollegeByCodeAsync(string code)
        {
            var colleges = await _repo.GetAsync(c => c.Code == code);
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
            var hasChildren = (await _deptRepo.GetAsync(d => d.CollegeId == id)).Any();
            if (hasChildren) throw new InvalidOperationException("Cannot delete College with associated Departments.");

            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _repo.DeleteAsync(entity);
        }
    }

    public class DepartmentService(IGenericRepository<Department> repo, IGenericRepository<Batch> batchRepo, ISmartStringGenerator smartString) : IDepartmentService
    {
        private readonly IGenericRepository<Department> _repo = repo;
        private readonly IGenericRepository<Batch> _batchRepo = batchRepo;
        private readonly ISmartStringGenerator _smartString = smartString;

        public async Task<IReadOnlyList<Department>> GetDepartmentsByCollegeIdAsync(Ulid collegeId)
            => await _repo.GetAsync(d => d.CollegeId == collegeId);

        public async Task<Department?> GetDepartmentByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<Department?> GetDepartmentByCodeAsync(string code)
        {
            var departments = await _repo.GetAsync(d => d.Code == code);
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
            var hasChildren = (await _batchRepo.GetAsync(b => b.DepartmentId == id)).Any();
            if (hasChildren) throw new InvalidOperationException("Cannot delete Department with associated Batches.");

            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _repo.DeleteAsync(entity);
        }
    }

    public class BatchService(IGenericRepository<Batch> repo, IGenericRepository<Group> groupRepo) : IBatchService
    {
        private readonly IGenericRepository<Batch> _repo = repo;
        private readonly IGenericRepository<Group> _groupRepo = groupRepo;

        public async Task<IReadOnlyList<Batch>> GetBatchesByDepartmentIdAsync(Ulid departmentId)
            => await _repo.GetAsync(b => b.DepartmentId == departmentId);

        public async Task<Batch?> GetBatchByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);
        public async Task<Batch?> GetBatchByCodeAsync(string code)
        {
            var batches = await _repo.GetAsync(b => b.Code == code);
            return batches.FirstOrDefault();
        }
        public async Task<Batch> CreateBatchAsync(Batch batch) => await _repo.AddAsync(batch);
        public async Task UpdateBatchAsync(Batch batch) => await _repo.UpdateAsync(batch);
        public async Task DeleteBatchAsync(Ulid id)
        {
            var hasChildren = (await _groupRepo.GetAsync(g => g.BatchId == id)).Any();
            if (hasChildren) throw new InvalidOperationException("Cannot delete Batch with associated Groups.");

            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _repo.DeleteAsync(entity);
        }
    }

    public class GroupService(IGenericRepository<Group> repo, IGenericRepository<Student> studentRepo) : IGroupService
    {
        private readonly IGenericRepository<Group> _repo = repo;
        private readonly IGenericRepository<Student> _studentRepo = studentRepo;

        public async Task<IReadOnlyList<Group>> GetGroupsByBatchIdAsync(Ulid batchId)
            => await _repo.GetAsync(g => g.BatchId == batchId);

        public async Task<Group?> GetGroupByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);
        public async Task<Group?> GetGroupByCodeAsync(string code)
        {
            var groups = await _repo.GetAsync(g => g.Code == code);
            return groups.FirstOrDefault();
        }
        public async Task<Group> CreateGroupAsync(Group group) => await _repo.AddAsync(group);
        public async Task UpdateGroupAsync(Group group) => await _repo.UpdateAsync(group);
        public async Task DeleteGroupAsync(Ulid id)
        {
            var hasChildren = (await _studentRepo.GetAsync(s => s.GroupId == id)).Any();
            if (hasChildren) throw new InvalidOperationException("Cannot delete Group with associated Students.");

            var entity = await _repo.GetByIdAsync(id);
            if (entity != null) await _repo.DeleteAsync(entity);
        }
    }
}
