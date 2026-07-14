using Domain.ValueObjects;

namespace Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    public User(Email email, PasswordHash passwordHash)
    {
        Id = Guid.NewGuid();
        Email = email ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePassword(PasswordHash newHash)
    {
        PasswordHash = newHash ?? throw new ArgumentNullException(nameof(newHash));
        UpdatedAt = DateTime.UtcNow;
    }
}