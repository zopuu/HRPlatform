using HRPlatform.DTOs;
using HRPlatform.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HRPlatform.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SkillsController : ControllerBase {
        private readonly ISkillsService _svc;
        public SkillsController(ISkillsService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SkillDto>>> GetAll([FromQuery] string ? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
            var result = await _svc.GetAsync(query, page, pageSize);
            Response.Headers["X-Total-Count"] = result.Total.ToString();
            return Ok(result);
        }
        [HttpGet("{id:int}")]
        public async Task<ActionResult<SkillDto>> GetOne(int id) => Ok(await _svc.GetByIdAsync(id));

        [HttpPost]
        public async Task<ActionResult<SkillDto>> Create([FromBody] SkillCreateRequest request) {
            var created = await _svc.CreateAsync(request);
            return CreatedAtAction(nameof(GetOne), new { id = created.Id }, created);
        }
        [HttpPut("{id:int}")]
        public async Task<ActionResult<SkillDto>> Update(int id, [FromBody] SkillUpdateRequest request) =>
            Ok(await _svc.UpdateAsync(id, request));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id) {
            await _svc.DeleteAsync(id);
            return NoContent();
        }
    }
}
