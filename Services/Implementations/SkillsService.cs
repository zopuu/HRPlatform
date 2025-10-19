using HRPlatform.Common.Errors;
using HRPlatform.Common.Types;
using HRPlatform.Data;
using HRPlatform.Domain;
using HRPlatform.DTOs;
using HRPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRPlatform.Services.Implementations {
    public class SkillsService : ISkillsService {
        private readonly AppDbContext _db;
        public SkillsService(AppDbContext db) => _db = db;

        public async Task<PagedResult<SkillDto>> GetAsync(string? query, int page, int pageSize) {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var s = _db.Skills.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query)) {
                var term = query.Trim();
                if (_db.Database.IsNpgsql())
                    s = s.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
                else
                    s = s.Where(x => x.Name.ToLower().Contains(term.ToLower())); // InMemory fallback for tests
            }
            var total = await s.CountAsync();
            var items = await s.OrderBy(s => s.Name)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Select(s => new SkillDto(s.Id, s.Name))
                               .ToListAsync();
            return new PagedResult<SkillDto>(items, total, page, pageSize);
        }
        public async Task<SkillDto> GetByIdAsync(int id) {
            var s = await _db.Skills.AsNoTracking()
                              .Where(s => s.Id == id)
                                .Select(s => new SkillDto(s.Id, s.Name))
                                .FirstOrDefaultAsync();
            if (s is null) throw new NotFoundException($"Skill {id} not found");
            return s;
        }
        public async Task<SkillDto> CreateAsync(SkillCreateRequest request) {
            var entity = new Skill { Name = request.Name.Trim() };
            _db.Skills.Add(entity);
            try {
                await _db.SaveChangesAsync();
            } catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
                throw new ConflictException($"Skill '{request.Name}' already exists.");
            }
            return new SkillDto(entity.Id, entity.Name);
        }
        public async Task<SkillDto> UpdateAsync(int id, SkillUpdateRequest request) {
            var entity = await _db.Skills.FindAsync(id);
            if (entity is null) throw new NotFoundException($"Skill {id} not found");
            entity.Name = request.Name.Trim();
            try {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
                throw new ConflictException($"Skill '{request.Name}' already exists.");
            }
            return new SkillDto(entity.Id, entity.Name);
        }
        public async Task DeleteAsync(int id) {
            var entity = await _db.Skills.FindAsync(id);
            if (entity is null) throw new NotFoundException($"Skill {id} not found");
            _db.Skills.Remove(entity);
            await _db.SaveChangesAsync();
        }
        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
    } 
}
