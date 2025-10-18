namespace HRPlatform.Domain {
    public class CandidateSkill {
        public int CandidateId { get; set; }
        public Candidate Candidate { get; set; } = default!;
        public int SkillId { get; set; }
        public Skill Skill { get; set; } = default!;
    }
}
