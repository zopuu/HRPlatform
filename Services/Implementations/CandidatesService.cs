using HRPlatform.Common.Errors;
using HRPlatform.Common.Types;
using HRPlatform.Data;
using HRPlatform.Domain;
using HRPlatform.DTOs;
using HRPlatform.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRPlatform.Services.Implementations {
    public class CandidatesService : ICandidatesService {
        private readonly AppDbContext _db;
        public CandidatesService(AppDbContext db) => _db = db;

        public async Task<PagedResult<CandidateDto>> GetAsync(

           string? name, List<int>? skillIds,
            string match,
            int page,
            int pageSize,
            string sortBy,
            string dir
            ) {

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            match = (match?.ToLower() == "all") ? "all" : "any";
            sortBy = sortBy?.ToLower() switch { "dob" => "dob", "email" => "email", "phone" => "phone", _ => "name" };
            dir = (dir?.ToLower() == "desc") ? "desc" : "asc";

            var q = _db.Candidates.AsNoTracking().AsQueryable();

            // Filtering
            if (!string.IsNullOrWhiteSpace(name)) {
                var term = name.Trim();
                if (_db.Database.IsNpgsql()) {
                    q = q.Where(c => EF.Functions.ILike(c.FullName, $"%{term}%"));
                }
                else {
                    q = q.Where(c => c.FullName.ToLower().Contains(term.ToLower()));    // for testing with InMemory db
                }
            }

            if (skillIds is { Count: > 0 }) {
                var set = skillIds.Distinct().ToList();
                if (match == "all") {
                    q = q.Where(c =>
                    c.CandidateSkills
                        .Where(cs => set.Contains(cs.SkillId))
                        .Select(cs => cs.SkillId)
                        .Distinct().Count() == set.Count);
                }
                else {
                    q = q.Where(c => c.CandidateSkills.Any(cs => set.Contains(cs.SkillId)));
                }
            }

            //sorting
            q = (sortBy, dir) switch {
                ("dob", "asc") => q.OrderBy(c => c.DateOfBirth),
                ("dob", "desc") => q.OrderByDescending(c => c.DateOfBirth),
                ("email", "asc") => q.OrderBy(c => c.Email),
                ("email", "desc") => q.OrderByDescending(c => c.Email),
                ("phone", "asc") => q.OrderBy(c => c.Phone),
                ("phone", "desc") => q.OrderByDescending(c => c.Phone),
                ("name", "desc") => q.OrderByDescending(c => c.FullName),
                _ => q.OrderBy(c => c.FullName),
            };
            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CandidateDto(
                    c.Id,
                    c.FullName,
                    c.DateOfBirth,
                    c.Email,
                    c.Phone,
                    c.CandidateSkills
                        .Select(cs => new SkillDto(
                            cs.Skill.Id,
                            cs.Skill.Name
                        ))
                        .ToList()
                ))
                .ToListAsync();
            return new PagedResult<CandidateDto>(items, total, page, pageSize);
        }
        public async Task<CandidateDto?> GetByIdAsync(int id) {
            var candidate = await _db.Candidates
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CandidateDto(
                    c.Id,
                    c.FullName,
                    c.DateOfBirth,
                    c.Email,
                    c.Phone,
                    c.CandidateSkills
                        .Select(cs => new SkillDto(
                            cs.Skill.Id,
                            cs.Skill.Name
                        ))
                        .OrderBy(s => s.Name)
                        .ToList()
                ))
                .FirstOrDefaultAsync();
            if (candidate is null) throw new NotFoundException($"Candidate {id} not found");
            return candidate;
        }
        public async Task<CandidateDto> CreateAsync(CandidateCreateRequest request) {
            var candidate = new Candidate {
                FullName = request.FullName,
                DateOfBirth = request.DateOfBirth,
                Email = request.Email,
                Phone = request.Phone
            };

            if (request.SkillIds is { Count: > 0 }) {
                var existingIds = await _db.Skills
                    .Where(s => request.SkillIds!.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync();
                var missing = request.SkillIds.Except(existingIds).ToList();
                if (missing.Count > 0) {
                    throw new NotFoundException($"Skills not found: {string.Join(", ", missing)}");
                }
                foreach (var sid in existingIds.Distinct())
                    candidate.CandidateSkills.Add(new CandidateSkill { SkillId = sid });
            }
            _db.Candidates.Add(candidate);
            try {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
                throw new ConflictException($"Candidate with email '{request.Email}' already exists.");
            }
            return await GetByIdAsync(candidate.Id) ?? throw new Exception("Failed to retrieve created candidate.");
        }
        public async Task<CandidateDto> UpdateAsync(int id, CandidateUpdateRequest request) {
            var entity = await _db.Candidates.FindAsync(id);
            if (entity is null) throw new NotFoundException($"Candidate {id} not found");

            entity.FullName = request.FullName.Trim();
            entity.DateOfBirth = request.DateOfBirth;
            entity.Phone = request.Phone.Trim();
            entity.Email = request.Email.Trim();

            try {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
                throw new ConflictException($"Email '{entity.Email}' is already in use.");
            }

            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(int id) {
            var entity = await _db.Candidates.FindAsync(id);
            if (entity is null) throw new NotFoundException($"Candidate {id} not found");

            _db.Candidates.Remove(entity);
            await _db.SaveChangesAsync();
        }

        public async Task<CandidateDto> AssignSkillsAsync(int candidateId, AssignSkillsRequest request) {
            if (request.SkillIds is not { Count: > 0 })
                return await GetByIdAsync(candidateId);

            var candidate = await _db.Candidates
                .Include(c => c.CandidateSkills)
                .FirstOrDefaultAsync(c => c.Id == candidateId);

            if (candidate is null) throw new NotFoundException($"Candidate {candidateId} not found");

            var existingSkills = await _db.Skills
                .Where(s => request.SkillIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();

            var missing = request.SkillIds.Except(existingSkills).ToList();
            if (missing.Count > 0)
                throw new NotFoundException($"Skill ids not found: {string.Join(",", missing)}");

            var already = candidate.CandidateSkills.Select(cs => cs.SkillId).ToHashSet();
            var toAdd = existingSkills.Where(id => !already.Contains(id));

            foreach (var sid in toAdd)
                candidate.CandidateSkills.Add(new CandidateSkill { SkillId = sid });

            await _db.SaveChangesAsync();
            return await GetByIdAsync(candidateId);
        }

        public async Task<CandidateDto> RemoveSkillAsync(int candidateId, int skillId) {
            var entity = await _db.Candidates
                .Include(c => c.CandidateSkills)
                .FirstOrDefaultAsync(c => c.Id == candidateId);

            if (entity is null) throw new NotFoundException($"Candidate {candidateId} not found");

            var link = entity.CandidateSkills.FirstOrDefault(cs => cs.SkillId == skillId);
            if (link is null) throw new NotFoundException($"Skill {skillId} not assigned to candidate {candidateId}");

            entity.CandidateSkills.Remove(link);
            await _db.SaveChangesAsync();
            return await GetByIdAsync(candidateId);
        }
        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    }
}
