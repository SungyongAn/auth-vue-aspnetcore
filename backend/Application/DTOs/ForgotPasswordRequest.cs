using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}