namespace Domain.ValueObjects;

public class PasswordHash
{
    public string Value { get; }

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Password hash cannot be empty.");

        Value = value;
    }

    public override string ToString() => Value;
}
