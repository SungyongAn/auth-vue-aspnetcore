namespace Application.DTOs;

public record RefreshResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresIn { get; init; }
}