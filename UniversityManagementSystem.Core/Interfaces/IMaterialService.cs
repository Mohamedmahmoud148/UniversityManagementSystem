using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IMaterialService
    {
        Task<MaterialDto> UploadMaterialAsync(Ulid offeringId, Ulid doctorId, IFormFile file);
        Task DeleteMaterialAsync(Ulid materialId, Ulid doctorId);
        Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(Ulid offeringId, Ulid callerId, string callerRole, int page, int pageSize, string? search);

        /// <summary>
        /// Returns the R2 storage key for a material the caller is allowed to access.
        /// Students require enrollment; Doctors and Admins bypass the enrollment check.
        /// </summary>
        Task<string> GetMaterialUrlAsync(Ulid materialId, Ulid callerId, string callerRole);
    }
}
