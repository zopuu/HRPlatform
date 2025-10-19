using FluentAssertions;
using HRPlatform.Common.Errors;
using HRPlatform.Data;
using HRPlatform.Domain;
using HRPlatform.DTOs;
using HRPlatform.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRPlatform.Tests {
    public class CandidatesService_UnitTests {
        private static AppDbContext CreateInMemoryDb(string? name = null) {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
                .Options;

            var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }
        [Fact]
        public async Task CreateAsync_adds_candidate() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            var req = new CandidateCreateRequest {
                FullName = "Ana Petrović",
                DateOfBirth = new DateOnly(1998, 5, 14),
                Phone = "+38164111222",
                Email = "ana.petrovic@example.com"
                // no skills
            };

            // ACT
            var dto = await sut.CreateAsync(req);

            // ASSERT
            dto.Id.Should().BeGreaterThan(0);
            dto.FullName.Should().Be("Ana Petrović");
            dto.DateOfBirth.Should().Be(new DateOnly(1998, 5, 14));
            dto.Phone.Should().Be("+38164111222");
            dto.Email.Should().Be("ana.petrovic@example.com");
            dto.Skills.Should().BeEmpty();

            (await db.Candidates.CountAsync()).Should().Be(1);
            var saved = await db.Candidates.SingleAsync();
            saved.FullName.Should().Be("Ana Petrović");
            saved.Email.Should().Be("ana.petrovic@example.com");
        }
        [Fact]
        public async Task GetByIdAsync_returns_candidate_with_assigned_skills() {
            // ARRANGE
            await using var db = CreateInMemoryDb();

            // seed two skills
            db.Skills.AddRange(
                new Skill { Name = "C#" },
                new Skill { Name = "Java" }
            );
            await db.SaveChangesAsync();

            var csharpId = await db.Skills.Where(s => s.Name == "C#").Select(s => s.Id).SingleAsync();
            var javaId = await db.Skills.Where(s => s.Name == "Java").Select(s => s.Id).SingleAsync();

            var sut = new CandidatesService(db);

            var create = new CandidateCreateRequest {
                FullName = "Marko Marković",
                DateOfBirth = new DateOnly(1995, 11, 2),
                Phone = "+38162123456",
                Email = "marko@example.com",
                SkillIds = new() { csharpId, javaId }
            };

            var created = await sut.CreateAsync(create);

            // ACT
            var dto = await sut.GetByIdAsync(created.Id);

            // ASSERT
            dto.Id.Should().Be(created.Id);
            dto.FullName.Should().Be("Marko Marković");
            dto.Skills.Should().HaveCount(2);
            dto.Skills.Select(s => s.Name).Should().BeEquivalentTo(new[] { "C#", "Java" });
            dto.Skills.Select(s => s.Name).Should().BeInAscendingOrder();
        }
        [Fact]
        public async Task UpdateAsync_updates_basic_fields() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            var created = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Ana Petrović",
                DateOfBirth = new DateOnly(1998, 5, 14),
                Phone = "+38164111222",
                Email = "ana.petrovic@example.com"
            });

            var req = new CandidateUpdateRequest {
                FullName = "Ana M. Petrović",
                DateOfBirth = new DateOnly(1997, 12, 1),
                Phone = "+38160123456",
                Email = "ana.m@example.com"
            };

            // ACT
            var updated = await sut.UpdateAsync(created.Id, req);

            // ASSERT
            updated.Id.Should().Be(created.Id);
            updated.FullName.Should().Be("Ana M. Petrović");
            updated.DateOfBirth.Should().Be(new DateOnly(1997, 12, 1));
            updated.Phone.Should().Be("+38160123456");
            updated.Email.Should().Be("ana.m@example.com");

            var inDb = await db.Candidates.FindAsync(created.Id);
            inDb!.FullName.Should().Be("Ana M. Petrović");
            inDb.DateOfBirth.Should().Be(new DateOnly(1997, 12, 1));
            inDb.Phone.Should().Be("+38160123456");
            inDb.Email.Should().Be("ana.m@example.com");
        }
        [Fact]
        public async Task DeleteAsync_removes_candidate_and_skill_links() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            // seed two skills so we also verify cascade removal of join rows
            db.Skills.AddRange(
                new Skill { Name = "C#" },
                new Skill { Name = "Java" }
            );
            await db.SaveChangesAsync();
            var skillIds = await db.Skills.Select(s => s.Id).ToListAsync();

            var created = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Test User",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "+38160000000",
                Email = "test.user@example.com",
                SkillIds = skillIds
            });

            // sanity pre-checks
            (await db.Candidates.AnyAsync(c => c.Id == created.Id)).Should().BeTrue();
            (await db.CandidateSkills.CountAsync()).Should().Be(skillIds.Count);

            // ACT
            await sut.DeleteAsync(created.Id);

            // ASSERT
            (await db.Candidates.AnyAsync(c => c.Id == created.Id)).Should().BeFalse();
            (await db.CandidateSkills.CountAsync()).Should().Be(0); // cascade cleared join rows
        }
        [Fact]
        public async Task AssignSkillsAsync_adds_missing_skills_and_is_idempotent() {
            // ARRANGE
            await using var db = CreateInMemoryDb();

            // seed skills
            db.Skills.AddRange(
                new HRPlatform.Domain.Skill { Name = "C#" },
                new HRPlatform.Domain.Skill { Name = "Java" },
                new HRPlatform.Domain.Skill { Name = "SQL" }
            );
            await db.SaveChangesAsync();
            var ids = await db.Skills.OrderBy(s => s.Name).Select(s => s.Id).ToListAsync();

            var sut = new CandidatesService(db);

            // create candidate with no skills
            var created = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Assign Tester",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Email = "assign@test.com",
                Phone = "+38160000000"
            });

            // ACT (first assign)
            var afterFirst = await sut.AssignSkillsAsync(
                created.Id,
                new AssignSkillsRequest { SkillIds = new() { ids[0], ids[1] } });

            // ASSERT (after first)
            afterFirst.Skills.Select(s => s.Name).Should().BeEquivalentTo(new[] { "C#", "Java" }, opts => opts.WithoutStrictOrdering());

            // ACT (assign again same + one new; should not duplicate existing)
            var afterSecond = await sut.AssignSkillsAsync(
                created.Id,
                new AssignSkillsRequest { SkillIds = new() { ids[1], ids[2] } });

            // ASSERT (idempotent; now should have 3 distinct)
            afterSecond.Skills.Select(s => s.Name).Should().BeEquivalentTo(new[] { "C#", "Java", "SQL" }, opts => opts.WithoutStrictOrdering());

            // DB assertions
            var links = await db.CandidateSkills.Where(cs => cs.CandidateId == created.Id).ToListAsync();
            links.Select(l => l.SkillId).Distinct().Should().HaveCount(3);
        }
        [Fact]
        public async Task RemoveSkillAsync_removes_link_and_keeps_others() {
            // ARRANGE
            await using var db = CreateInMemoryDb();

            // seed skills
            db.Skills.AddRange(
                new Skill { Name = "C#" },
                new Skill { Name = "Java" }
            );
            await db.SaveChangesAsync();
            var skillIds = await db.Skills.OrderBy(s => s.Name).Select(s => s.Id).ToListAsync();
            var csharpId = skillIds[0];
            var javaId = skillIds[1];

            var sut = new CandidatesService(db);

            // create candidate WITH both skills
            var created = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Remove Tester",
                DateOfBirth = new DateOnly(1992, 2, 2),
                Email = "remove@test.com",
                Phone = "+38161111111",
                SkillIds = new() { csharpId, javaId }
            });

            // sanity pre-check
            (await db.CandidateSkills.CountAsync(cs => cs.CandidateId == created.Id)).Should().Be(2);

            // ACT (remove one)
            var after = await sut.RemoveSkillAsync(created.Id, csharpId);

            // ASSERT
            after.Skills.Select(s => s.Name).Should().BeEquivalentTo(new[] { "Java" });
            (await db.CandidateSkills
                .Where(cs => cs.CandidateId == created.Id)
                .Select(cs => cs.SkillId)
                .ToListAsync())
                .Should().BeEquivalentTo(new[] { javaId });

            // optional: removing a non-assigned skill should throw NotFound
            var act = async () => await sut.RemoveSkillAsync(created.Id, csharpId);
            await act.Should().ThrowAsync<NotFoundException>();
        }
        [Fact]
        public async Task GetAsync_filters_by_name_case_insensitive() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Ana Petrović",
                DateOfBirth = new DateOnly(1998, 5, 14),
                Phone = "+38160000001",
                Email = "ana@example.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Marko Marković",
                DateOfBirth = new DateOnly(1995, 11, 2),
                Phone = "+38160000002",
                Email = "marko@example.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Anastasija Jovanović",
                DateOfBirth = new DateOnly(1997, 3, 7),
                Phone = "+38160000003",
                Email = "anastasija@example.com"
            });

            // ACT
            var page = await sut.GetAsync(
                name: "ANA",              // mixed case
                skillIds: null,
                match: "any",
                page: 1, pageSize: 10,
                sortBy: "name", dir: "asc");

            // ASSERT
            var names = page.Items.Select(c => c.FullName).ToList();
            names.Should().Contain("Ana Petrović");
            names.Should().Contain("Anastasija Jovanović");
            names.Should().NotContain("Marko Marković");
        }
        [Fact]
        public async Task GetAsync_filters_by_skills_match_any() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            // seed skills
            db.Skills.AddRange(
                new Skill { Name = "C#" },
                new Skill { Name = "SQL" },
                new Skill { Name = "Java" }
            );
            await db.SaveChangesAsync();

            var csharpId = await db.Skills.Where(s => s.Name == "C#").Select(s => s.Id).SingleAsync();
            var sqlId = await db.Skills.Where(s => s.Name == "SQL").Select(s => s.Id).SingleAsync();
            var javaId = await db.Skills.Where(s => s.Name == "Java").Select(s => s.Id).SingleAsync();

            // A: C#
            var a = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "A",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "1",
                Email = "a@ex.com",
                SkillIds = new() { csharpId }
            });
            // B: Java + SQL
            var b = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "B",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "2",
                Email = "b@ex.com",
                SkillIds = new() { javaId, sqlId }
            });
            // C: none
            var c = await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "C",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "3",
                Email = "c@ex.com"
            });

            // ACT: any of {C#, SQL} => should return A and B
            var page = await sut.GetAsync(
                name: null,
                skillIds: new() { csharpId, sqlId },
                match: "any",
                page: 1, pageSize: 10,
                sortBy: "name", dir: "asc");

            // ASSERT
            page.Items.Select(x => x.FullName).Should().BeEquivalentTo(new[] { "A", "B" });
        }
        [Fact]
        public async Task GetAsync_filters_by_skills_match_all() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            // seed skills
            db.Skills.AddRange(
                new HRPlatform.Domain.Skill { Name = "C#" },
                new HRPlatform.Domain.Skill { Name = "SQL" },
                new HRPlatform.Domain.Skill { Name = "Java" }
            );
            await db.SaveChangesAsync();

            var csharpId = await db.Skills.Where(s => s.Name == "C#").Select(s => s.Id).SingleAsync();
            var sqlId = await db.Skills.Where(s => s.Name == "SQL").Select(s => s.Id).SingleAsync();
            var javaId = await db.Skills.Where(s => s.Name == "Java").Select(s => s.Id).SingleAsync();

            // A: C#
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "A",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "1",
                Email = "a@ex.com",
                SkillIds = new() { csharpId }
            });
            // B: Java + SQL (the only one that matches all {Java, SQL})
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "B",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "2",
                Email = "b@ex.com",
                SkillIds = new() { javaId, sqlId }
            });
            // C: none
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "C",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "3",
                Email = "c@ex.com"
            });

            // ACT (all)
            var page = await sut.GetAsync(
                name: null,
                skillIds: new() { javaId, sqlId },
                match: "all",
                page: 1, pageSize: 10,
                sortBy: "name", dir: "asc");

            // ASSERT
            page.Items.Select(x => x.FullName).Should().BeEquivalentTo(new[] { "B" });
        }
        [Fact]
        public async Task GetAsync_sorts_by_dob_both_directions() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Oldest",
                DateOfBirth = new DateOnly(1980, 1, 1),
                Phone = "1",
                Email = "oldest@ex.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Middle",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "2",
                Email = "middle@ex.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "Youngest",
                DateOfBirth = new DateOnly(2000, 1, 1),
                Phone = "3",
                Email = "youngest@ex.com"
            });

            // ACT
            var asc = await sut.GetAsync(null, null, "any", 1, 10, "dob", "asc");
            var desc = await sut.GetAsync(null, null, "any", 1, 10, "dob", "desc");

            // ASSERT
            asc.Items.Select(x => x.FullName).Should().ContainInOrder("Oldest", "Middle", "Youngest");
            desc.Items.Select(x => x.FullName).Should().ContainInOrder("Youngest", "Middle", "Oldest");
        }
        [Fact]
        public async Task GetAsync_sorts_by_email_desc_and_phone_asc() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new CandidatesService(db);

            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "A",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "3",
                Email = "a@zzz.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "B",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "1",
                Email = "b@mmm.com"
            });
            await sut.CreateAsync(new CandidateCreateRequest {
                FullName = "C",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Phone = "2",
                Email = "c@aaa.com"
            });

            // ACT
            var byEmailDesc = await sut.GetAsync(null, null, "any", 1, 10, "email", "desc");
            var byPhoneAsc = await sut.GetAsync(null, null, "any", 1, 10, "phone", "asc");

            // ASSERT
            byEmailDesc.Items.Select(x => x.Email).Should().ContainInOrder("c@aaa.com", "b@mmm.com", "a@zzz.com"); // desc by string sorts z..a, but our sample domains invert; check actual order
            byPhoneAsc.Items.Select(x => x.Phone).Should().ContainInOrder("1", "2", "3");
        }


    }
}
