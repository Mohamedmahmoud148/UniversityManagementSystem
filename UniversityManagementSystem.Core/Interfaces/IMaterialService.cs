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
        Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(Ulid offeringId, Ulid studentId, int page, int pageSize, string? search);

        /// <summary>
        /// Returns the R2 public URL for a material the student is allowed to access.
        /// </summary>
        Task<string> GetMaterialUrlAsync(Ulid materialId, Ulid studentId);
    }
}
