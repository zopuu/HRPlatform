using HRPlatform.Common.Types;
using HRPlatform.DTOs;

namespace HRPlatform.Services.Interfaces {
    public interface ICandidatesService {
        Task<PagedResult<CandidateDto>> GetAsync(
            string? name,
            List<int>? skillIds,
            string match,
            int page,
            int pageSize,
            string sortBy,
            string dir
            );
        Task<CandidateDto?> GetByIdAsync(int id);
        Task<CandidateDto> CreateAsync(CandidateCreateRequest request);
        Task<CandidateDto?> UpdateAsync(int id, CandidateUpdateRequest request);
        Task DeleteAsync(int id);

        Task<CandidateDto> AssignSkillsAsync(int candidateId, AssignSkillsRequest request);
        Task<CandidateDto> RemoveSkillAsync(int candidateId, int skillId);
    }
}
