namespace Application.DTOs;

public class RefreshResponse
{
    public required string AccessToken { get; set; }
    public int ExpiresIn { get; set; }
}
