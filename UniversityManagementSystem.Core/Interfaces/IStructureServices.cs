using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IUniversityService
    {
        Task<IReadOnlyList<University>> GetAllUniversitiesAsync();
        Task<University?> GetUniversityByIdAsync(int id);
        Task<University> CreateUniversityAsync(University university);
        Task UpdateUniversityAsync(University university);
        Task DeleteUniversityAsync(int id);
    }

    public interface ICollegeService
    {
        Task<IReadOnlyList<College>> GetAllCollegesAsync();
        Task<IReadOnlyList<College>> GetCollegesByUniversityIdAsync(int universityId);
        Task<College?> GetCollegeByIdAsync(int id);
        Task<College?> GetCollegeByPublicIdAsync(string publicId);
        Task<College> CreateCollegeAsync(College college);
        Task UpdateCollegeAsync(College college);
        Task DeleteCollegeAsync(int id);
    }

    public interface IDepartmentService
    {
        Task<IReadOnlyList<Department>> GetDepartmentsByCollegeIdAsync(int collegeId);
        Task<Department?> GetDepartmentByIdAsync(int id);
        Task<Department?> GetDepartmentByPublicIdAsync(string publicId);
        Task<Department> CreateDepartmentAsync(Department department);
        Task UpdateDepartmentAsync(Department department);
        Task DeleteDepartmentAsync(int id);
    }

    public interface IBatchService
    {
        Task<IReadOnlyList<Batch>> GetBatchesByDepartmentIdAsync(int departmentId);
        Task<Batch?> GetBatchByIdAsync(int id);
        Task<Batch> CreateBatchAsync(Batch batch);
        Task UpdateBatchAsync(Batch batch);
        Task DeleteBatchAsync(int id);
    }

    public interface IGroupService
    {
        Task<IReadOnlyList<Group>> GetGroupsByBatchIdAsync(int batchId);
        Task<Group?> GetGroupByIdAsync(int id);
        Task<Group> CreateGroupAsync(Group group);
        Task UpdateGroupAsync(Group group);
        Task DeleteGroupAsync(int id);
    }
}
