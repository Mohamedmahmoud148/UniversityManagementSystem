using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IUniversityService
    {
        Task<IReadOnlyList<University>> GetAllUniversitiesAsync();
        Task<University?> GetUniversityByIdAsync(Ulid id);
        Task<University> CreateUniversityAsync(University university);
        Task UpdateUniversityAsync(University university);
        Task DeleteUniversityAsync(Ulid id);
    }

    public interface ICollegeService
    {
        Task<IReadOnlyList<College>> GetAllCollegesAsync();
        Task<IReadOnlyList<College>> GetCollegesByUniversityIdAsync(Ulid universityId);
        Task<College?> GetCollegeByIdAsync(Ulid id);
        Task<College?> GetCollegeByCodeAsync(string code);
        Task<College> CreateCollegeAsync(College college);
        Task UpdateCollegeAsync(College college);
        Task DeleteCollegeAsync(Ulid id);
    }

    public interface IDepartmentService
    {
        Task<IReadOnlyList<Department>> GetDepartmentsByCollegeIdAsync(Ulid collegeId);
        Task<Department?> GetDepartmentByIdAsync(Ulid id);
        Task<Department?> GetDepartmentByCodeAsync(string code);
        Task<Department> CreateDepartmentAsync(Department department);
        Task UpdateDepartmentAsync(Department department);
        Task DeleteDepartmentAsync(Ulid id);
    }

    public interface IBatchService
    {
        Task<IReadOnlyList<Batch>> GetBatchesByDepartmentIdAsync(Ulid departmentId);
        Task<Batch?> GetBatchByIdAsync(Ulid id);
        Task<Batch?> GetBatchByCodeAsync(string code);
        Task<Batch> CreateBatchAsync(Batch batch);
        Task UpdateBatchAsync(Batch batch);
        Task DeleteBatchAsync(Ulid id);
    }

    public interface IGroupService
    {
        Task<IReadOnlyList<Group>> GetGroupsByBatchIdAsync(Ulid batchId);
        Task<Group?> GetGroupByIdAsync(Ulid id);
        Task<Group?> GetGroupByCodeAsync(string code);
        Task<Group> CreateGroupAsync(Group group);
        Task UpdateGroupAsync(Group group);
        Task DeleteGroupAsync(Ulid id);
    }
}
