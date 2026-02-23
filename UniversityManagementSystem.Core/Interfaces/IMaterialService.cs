using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IMaterialService
    {
        Task<MaterialDto> UploadMaterialAsync(int offeringId, int doctorId, IFormFile file);
        Task DeleteMaterialAsync(int materialId, int doctorId);
        Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(int offeringId, int studentId, int page, int pageSize, string? search);
        Task<(string FilePath, string ContentType, string FileName, DateTime LastModified)> GetMaterialFileInfoAsync(int materialId, int studentId);
    }
}
