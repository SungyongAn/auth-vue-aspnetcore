// Application/DTOs/UserInfoResponse.cs
namespace Application.DTOs;

public record UserInfoResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
}