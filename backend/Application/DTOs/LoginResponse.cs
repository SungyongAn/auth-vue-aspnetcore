namespace Application.DTOs;

public record LoginResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}