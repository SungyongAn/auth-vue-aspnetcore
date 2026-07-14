namespace Infrastructure.Services;

public class JwtOptions
{
    public required string Key { get; set; }
    public int AccessTokenExpiresInMinutes { get; set; } = 15;
    public int RefreshTokenExpiresInDays { get; set; } = 14;
}