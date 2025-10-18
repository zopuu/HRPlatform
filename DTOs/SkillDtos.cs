using System.ComponentModel.DataAnnotations;
namespace HRPlatform.DTOs;

public record SkillDto( int Id, string Name );
public class SkillCreateRequest {
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
public class SkillUpdateRequest {
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;    
}
