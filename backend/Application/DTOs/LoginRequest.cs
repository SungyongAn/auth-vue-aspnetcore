using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}