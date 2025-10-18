using Microsoft.EntityFrameworkCore;
using HRPlatform.Domain;

namespace HRPlatform.Data {
    public class AppDbContext : DbContext {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Candidate> Candidates => Set<Candidate>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.HasPostgresExtension("citext");

            modelBuilder.Entity<Candidate>(entity => {
                entity.ToTable("candidates");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.FullName).HasMaxLength(80).IsRequired();
                entity.Property(e => e.DateOfBirth).HasColumnName("date").IsRequired();
                entity.Property(e => e.Phone).HasMaxLength(20).IsRequired();

                entity.Property(e => e.Email).IsRequired().HasColumnType("citext");
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Skill>(entity => {
                entity.ToTable("skills");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired().HasColumnType("citext");
                entity.HasIndex(e => e.Name).IsUnique();
            });
            modelBuilder.Entity<CandidateSkill>(entity => {
                entity.ToTable("candidate_skills");
                entity.HasKey(e => new { e.CandidateId, e.SkillId });
                entity.HasOne(cs => cs.Candidate)
                      .WithMany(c => c.CandidateSkills)
                      .HasForeignKey(cs => cs.CandidateId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(cs => cs.Skill)
                      .WithMany(s => s.CandidateSkills)
                      .HasForeignKey(cs => cs.SkillId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
