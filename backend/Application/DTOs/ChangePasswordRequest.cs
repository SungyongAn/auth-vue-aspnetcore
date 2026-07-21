// Application/DTOs/ChangePasswordRequest.cs
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}