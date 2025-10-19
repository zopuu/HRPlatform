using FluentAssertions;
using HRPlatform.Data;
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
                // SkillIds omitted on purpose
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
    }
}
