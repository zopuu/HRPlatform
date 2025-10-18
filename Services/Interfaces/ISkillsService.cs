using HRPlatform.Common.Types;
using HRPlatform.DTOs;

namespace HRPlatform.Services.Interfaces {
    public interface ISkillsService {
        Task<PagedResult<SkillDto>> GetAsync(string? query,int page,int pageSize);
        Task<SkillDto> GetByIdAsync(int id);
        Task<SkillDto> CreateAsync(SkillCreateRequest request);
        Task<SkillDto> UpdateAsync(int id, SkillUpdateRequest request);
        Task DeleteAsync(int id);
    }
}
