namespace TaskManager.Application.Common;

public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

public sealed class EmailAlreadyInUseException : ConflictException
{
    public EmailAlreadyInUseException()
        : base("An account with this email already exists.")
    {
    }
}

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}
