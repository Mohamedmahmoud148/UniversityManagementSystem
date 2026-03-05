using System;
using System.Collections.Generic;
using System.IO;
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
        Task<(string FilePath, string ContentType, string FileName, DateTime LastModified)> GetMaterialFileInfoAsync(Ulid materialId, Ulid studentId);
    }
}
