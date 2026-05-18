using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IRegistrationService
    {
        /// <summary>
        /// Returns all offerings for a semester with eligibility status per offering.
        /// The student sees which they can and cannot register — with reasons.
        /// </summary>
        Task<IReadOnlyList<EligibleOfferingDto>> GetEligibleOfferingsAsync(Ulid studentId, Ulid semesterId);

        /// <summary>
        /// Validates and creates an enrollment, or adds the student to the waitlist if full.
        /// Returns details about what happened.
        /// </summary>
        Task<EnrollmentResultDto> EnrollAsync(Ulid studentId, Ulid offeringId);

        /// <summary>
        /// Adds student to the waitlist for a full offering.
        /// </summary>
        Task<WaitlistResultDto> JoinWaitlistAsync(Ulid studentId, Ulid offeringId);

        /// <summary>
        /// Removes student from the waitlist.
        /// </summary>
        Task LeaveWaitlistAsync(Ulid studentId, Ulid offeringId);
    }
}
