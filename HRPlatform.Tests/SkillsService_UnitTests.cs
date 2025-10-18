using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRPlatform.Common.Errors;
using HRPlatform.Data;
using HRPlatform.Domain;
using HRPlatform.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace HRPlatform.Tests {
    public class SkillsService_UnitTests {
        private static AppDbContext CreateInMemoryDb(string? name = null) {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
                .Options;

            var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        [Fact]
        public async Task CreateAsync_adds_skill() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new SkillsService(db);

            // ACT
            var dto = await sut.CreateAsync(new() { Name = "C#" });

            // ASSERT
            dto.Id.Should().BeGreaterThan(0);
            dto.Name.Should().Be("C#");
            (await db.Skills.CountAsync()).Should().Be(1);
            (await db.Skills.SingleAsync()).Name.Should().Be("C#");
        }

        [Fact]
        public async Task GetByIdAsync_returns_skill_when_exists() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new SkillsService(db);
            var created = await sut.CreateAsync(new() { Name = "SQL" });

            // ACT
            var dto = await sut.GetByIdAsync(created.Id);

            // ASSERT
            dto.Id.Should().Be(created.Id);
            dto.Name.Should().Be("SQL");
        }

        [Fact]
        public async Task GetByIdAsync_throws_NotFound_when_missing() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new SkillsService(db);

            // ACT
            Func<Task> act = async () => await sut.GetByIdAsync(999);

            // ASSERT
            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task UpdateAsync_changes_name() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new SkillsService(db);
            var created = await sut.CreateAsync(new() { Name = "Node" });

            // ACT
            var updated = await sut.UpdateAsync(created.Id, new() { Name = "Node.js" });

            // ASSERT
            updated.Id.Should().Be(created.Id);
            updated.Name.Should().Be("Node.js");
            (await db.Skills.FindAsync(created.Id))!.Name.Should().Be("Node.js");
        }

        [Fact]
        public async Task DeleteAsync_removes_entity() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            var sut = new SkillsService(db);
            var s = await sut.CreateAsync(new() { Name = "Prolog" });

            // ACT
            await sut.DeleteAsync(s.Id);

            // ASSERT
            (await db.Skills.AnyAsync(x => x.Id == s.Id)).Should().BeFalse();
        }

        [Fact]
        public async Task GetAsync_returns_paged_sorted_results_when_no_query() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            db.Skills.AddRange(
                new Skill { Name = "C" },
                new Skill { Name = "A" },
                new Skill { Name = "B" }
            );
            await db.SaveChangesAsync();
            var sut = new SkillsService(db);

            // ACT
            var page1 = await sut.GetAsync(query: null, page: 1, pageSize: 2);
            var page2 = await sut.GetAsync(query: null, page: 2, pageSize: 2);

            // ASSERT
            page1.Total.Should().Be(3);
            page1.Items.Select(i => i.Name).Should().ContainInOrder("A", "B");
            page2.Items.Select(i => i.Name).Should().ContainSingle().Which.Should().Be("C");
        }

        [Fact]
        public async Task GetAsync_normalizes_page_and_pageSize_bounds() {
            // ARRANGE
            await using var db = CreateInMemoryDb();
            db.Skills.AddRange(
                new Skill { Name = "A" },
                new Skill { Name = "B" },
                new Skill { Name = "C" }
            );
            await db.SaveChangesAsync();
            var sut = new SkillsService(db);

            // ACT
            var result = await sut.GetAsync(query: null, page: 0, pageSize: 999); // page < 1 -> 1, pageSize > 100 -> 20

            // ASSERT
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            result.Total.Should().Be(3);
            result.Items.Should().HaveCount(3 <= 20 ? 3 : 20);
        }
    }
}
