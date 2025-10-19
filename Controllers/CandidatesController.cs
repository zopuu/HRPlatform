using HRPlatform.DTOs;
using HRPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace HRPlatform.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CandidatesController : ControllerBase {
        private readonly ICandidatesService _svc;
        public CandidatesController(ICandidatesService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CandidateDto>>> Get(

            [FromQuery] string? name,
            [FromQuery] string? skills,
            [FromQuery] string match = "any",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = "name",
            [FromQuery] string dir = "asc"
            ) {
            var skillIds = string.IsNullOrWhiteSpace(skills)
                ? null
                : skills.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                                        .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            // If 'skills' parameter is provided but no valid IDs parsed, return 400 Bad Request
            if (!string.IsNullOrWhiteSpace(skills) && (skillIds == null || skillIds.Count == 0))
                return BadRequest(new ProblemDetails {
                    Title = "Invalid 'skills' value",
                    Detail = "Use comma-separated skill IDs, e.g. ?skills=1,2,3"
                });
            var result = await _svc.GetAsync(name, skillIds, match, page, pageSize, sortBy, dir);
            Response.Headers["X-Total-Count"] = result.Total.ToString();
            return Ok(result.Items);
        }
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CandidateDto>> GetOne(int id) => Ok(await _svc.GetByIdAsync(id));

        [HttpPost]
        public async Task<ActionResult<CandidateDto>> Create([FromBody] CandidateCreateRequest request) {
            var created = await _svc.CreateAsync(request);
            return CreatedAtAction(nameof(GetOne), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CandidateDto>> Update(int id, [FromBody] CandidateUpdateRequest request) =>
            Ok(await _svc.UpdateAsync(id, request));
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id) {
            await _svc.DeleteAsync(id);
            return NoContent();
        }
        [HttpPost("{candidateId:int}/skills")]
        public async Task<ActionResult<CandidateDto>> AssignSkills(int candidateId, [FromBody] AssignSkillsRequest request) =>
            Ok(await _svc.AssignSkillsAsync(candidateId, request));
        [HttpDelete("{candidateId:int}/skills/{skillId:int}")]
        public async Task<ActionResult<CandidateDto>> RemoveSkill(int candidateId, int skillId) =>
            Ok(await _svc.RemoveSkillAsync(candidateId, skillId));
    }
}
