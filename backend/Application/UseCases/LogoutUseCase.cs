using Application.Interfaces;

namespace Application.UseCases;

public class LogoutUseCase
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly ITokenService _tokenService;

    public LogoutUseCase(IRefreshTokenRepository tokens, ITokenService tokenService)
    {
        _tokens = tokens;
        _tokenService = tokenService;
    }

    public async Task ExecuteAsync(string rawRefreshToken)
    {
        var hashed = _tokenService.HashToken(rawRefreshToken);

        var token = await _tokens.GetValidTokenAsync(hashed);
        if (token == null)
        {
            return;
        }

        await _tokens.RevokeAsync(token);
    }
}