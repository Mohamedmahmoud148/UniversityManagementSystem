using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Application.AI.Contracts;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.AI.Tools;

public class ResolveSubjectOfferingTool : IAiTool
{
    private readonly AppDbContext _context;

    public ResolveSubjectOfferingTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => "ResolveSubjectOffering";

    public async Task<object> ExecuteAsync(object parameters, ClaimsPrincipal user)
    {
        var paramJson = JsonSerializer.Serialize(parameters);
        var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramJson)
            ?? throw new ArgumentException("Invalid parameters format");

        string collegeName = paramDict.TryGetValue("collegeName", out var c) ? c.GetString() ?? "" : "";
        string departmentName = paramDict.TryGetValue("departmentName", out var d) ? d.GetString() ?? "" : "";
        string batchName = paramDict.TryGetValue("batchName", out var b) ? b.GetString() ?? "" : "";
        string subjectName = paramDict.TryGetValue("subjectName", out var s) ? s.GetString() ?? "" : "";

        var nameIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("nameid")?.Value;

        if (string.IsNullOrEmpty(nameIdClaim) || !Ulid.TryParse(nameIdClaim, out var doctorSystemUserId))
            throw new UnauthorizedAccessException("Doctor ID not found in token.");

        var offering = await _context.SubjectOfferings
            .Include(o => o.Department)
                .ThenInclude(dep => dep.College)
            .Include(o => o.Batch)
            .Include(o => o.Subject)
            .Include(o => o.Doctor)
            .FirstOrDefaultAsync(o =>
                o.Department.College.Name.ToLower() == collegeName.ToLower() &&
                o.Department.Name.ToLower() == departmentName.ToLower() &&
                o.Batch.Name.ToLower() == batchName.ToLower() &&
                o.Subject.Name.ToLower() == subjectName.ToLower()
            );

        if (offering == null)
        {
            return new { error = "Target subject offering could not be found." };
        }

        if (offering.Doctor.SystemUserId != doctorSystemUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized. You do not own this subject offering.");
        }

        return new
        {
            subjectOfferingId = offering.Id
        };
    }
}
