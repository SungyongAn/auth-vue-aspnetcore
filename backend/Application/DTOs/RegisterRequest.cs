using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record RegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}