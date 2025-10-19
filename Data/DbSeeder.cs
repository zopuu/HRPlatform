using HRPlatform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRPlatform.Data;

public static class DbSeeder {
    public static async Task SeedAsync(AppDbContext db, ILogger logger) {
        // 1) Ensure skills (insert missing ones only)
        var seedSkills = new[]
        {
            "C#", "Java", "SQL", "JavaScript", "React", "ASP.NET Core", "Docker", "PostgreSQL"
        };

        foreach (var name in seedSkills) {
            var exists = await db.Skills.AnyAsync(s =>
                db.Database.IsNpgsql()
                    ? s.Name == name            // citext == is case-insensitive on Postgres
                    : s.Name.ToLower() == name.ToLower());

            if (!exists)
                db.Skills.Add(new Skill { Name = name });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded/ensured skills: {Count}", seedSkills.Length);

        // Preload to map names->ids for linking
        var skillIdByName = await db.Skills
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase);

        // 2) Ensure candidates + links (insert/merge)
        await EnsureCandidateAsync(db, logger,
            fullName: "Mirko Poledica", dob: new DateOnly(2002, 5, 26),
            email: "mirkop@example.com", phone: "225883",
            skills: new[] { "C#", "PostgreSQL" });

        await EnsureCandidateAsync(db, logger,
            fullName: "Ana Petrović", dob: new DateOnly(1997, 12, 1),
            email: "ana@example.com", phone: "+38160123456",
            skills: new[] { "ASP.NET Core", "C#", "Docker" });

        await EnsureCandidateAsync(db, logger,
            fullName: "Marko Marković", dob: new DateOnly(1995, 11, 2),
            email: "marko@example.com", phone: "+38162123456",
            skills: new[] { "Java", "SQL" });

        // local helper
        async Task EnsureCandidateAsync(AppDbContext dbx, ILogger log, string fullName, DateOnly dob, string email, string phone, string[] skills) {
            var cand = await dbx.Candidates
                .Include(c => c.CandidateSkills)
                .FirstOrDefaultAsync(c =>
                    dbx.Database.IsNpgsql()
                        ? c.Email == email      // citext
                        : c.Email.ToLower() == email.ToLower());

            if (cand is null) {
                cand = new Candidate { FullName = fullName, DateOfBirth = dob, Email = email, Phone = phone };
                dbx.Candidates.Add(cand);
                await dbx.SaveChangesAsync();
                log.LogInformation("Seeded candidate {Email}", email);
            }

            var existing = cand.CandidateSkills.Select(cs => cs.SkillId).ToHashSet();
            foreach (var name in skills.Distinct(StringComparer.OrdinalIgnoreCase)) {
                if (skillIdByName.TryGetValue(name, out var sid) && !existing.Contains(sid))
                    cand.CandidateSkills.Add(new CandidateSkill { SkillId = sid });
            }
            await dbx.SaveChangesAsync();
        }
    }
}
