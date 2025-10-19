using FluentAssertions;
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
                new HRPlatform.Domain.Skill { Name = "C#" },
                new HRPlatform.Domain.Skill { Name = "Java" }
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

    }
}
