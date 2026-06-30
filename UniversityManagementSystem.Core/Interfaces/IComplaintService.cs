using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IComplaintService
    {
        Task<ComplaintDto> CreateComplaintAsync(Ulid studentId, CreateComplaintDto dto);
        Task<ComplaintsPageDto> GetComplaintsAsync(GetComplaintsQueryDto query, Ulid callerId, string callerRole);
        Task<ComplaintDto> GetComplaintByIdAsync(Ulid complaintId, Ulid callerId, string callerRole);
        
        Task<ComplaintDto> ReplyToComplaintAsync(Ulid complaintId, string reply, Ulid doctorSystemUserId);
        Task<List<ComplaintClusterDto>> GetClustersAsync(string? targetType, string? targetId);
        Task<List<DoctorOptionDto>> GetDoctorOptionsForStudentAsync(Ulid studentId);

        Task<ClusterReplyResponseDto> ReplyToClusterAsync(Ulid clusterId, string message, Ulid repliedByUserId);
        Task<EnhancedComplaintClusterDto> GetClusterByIdAsync(Ulid clusterId);
        Task UpdateClusterStatusAsync(Ulid clusterId, string newStatus, Ulid changedByUserId, string? reason);
        Task<ComplaintDashboardDto> GetDashboardAsync();
        Task<int> ReprocessUnanalyzedComplaintsAsync();
    }
}
