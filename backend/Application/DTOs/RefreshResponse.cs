namespace Application.DTOs;

public record RefreshResponse
{
    public required string AccessToken { get; init; }
    public int ExpiresIn { get; init; }
}