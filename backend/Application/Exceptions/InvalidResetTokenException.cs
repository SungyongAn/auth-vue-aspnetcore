namespace Application.Exceptions;

public class InvalidResetTokenException : Exception
{
    public InvalidResetTokenException() : base("Invalid or expired reset token.") { }
}