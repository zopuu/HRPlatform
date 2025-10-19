using System.ComponentModel.DataAnnotations;

namespace HRPlatform.DTOs;

public record CandidateDto(
    int Id,
    string FullName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    IReadOnlyList<SkillDto> Skills
);
public class CandidateCreateRequest {
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public DateOnly DateOfBirth { get; set; }
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required,MaxLength(32)]
    public string Phone { get; set; } = string.Empty;
    // optional on create
    public List<int>? SkillIds { get; set; } = [];
}
public class CandidateUpdateRequest {
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public DateOnly DateOfBirth { get; set; }
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required,MaxLength(32)]
    public string Phone { get; set; } = string.Empty;
}
public class AssignSkillsRequest {
    [Required]
    public List<int> SkillIds { get; set; } = [];
}