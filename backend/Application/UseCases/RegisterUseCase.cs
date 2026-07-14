using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UseCases;

public class RegisterUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly LoginUseCase _loginUseCase;

    public RegisterUseCase(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        LoginUseCase loginUseCase)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _loginUseCase = loginUseCase;
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ExecuteAsync(RegisterRequest request)
    {
        var email = new Email(request.Email);

        var existing = await _users.GetByEmailAsync(email);
        if (existing != null)
            throw new EmailAlreadyExistsException();

        var passwordHash = new PasswordHash(_passwordHasher.Hash(request.Password));
        var user = new User(email, passwordHash);
        await _users.AddAsync(user);

        // ログイン処理を委譲（自動ログイン）
        return await _loginUseCase.ExecuteAsync(new LoginRequest
        {
            Email = request.Email,
            Password = request.Password
        });
    }
}