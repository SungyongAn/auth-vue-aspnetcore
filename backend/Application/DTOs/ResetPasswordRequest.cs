using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record ResetPasswordRequest
{
    [Required]
    public required string Token { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}