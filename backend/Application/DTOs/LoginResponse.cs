namespace Application.DTOs;

public class LoginResponse
{
    public required string AccessToken { get; set; }
    public int ExpiresIn { get; set; }
    public Guid UserId { get; set; }
    public required string Email { get; set; }
}
