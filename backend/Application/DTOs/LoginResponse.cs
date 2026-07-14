namespace Application.DTOs;

public record LoginResponse
{
    public required string AccessToken { get; init; }
    public int ExpiresIn { get; init; }
    public Guid UserId { get; init; }
    public required string Email { get; init; }
}