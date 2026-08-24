using FluentValidation;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Application.Users;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokens;
    private readonly IClock _clock;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        ITokenGenerator tokens,
        IClock clock,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = Email.Create(request.Email);

        if (await _users.ExistsWithEmailAsync(email, cancellationToken))
        {
            throw new EmailAlreadyInUseException();
        }

        var user = User.Register(request.Email, request.DisplayName, _hasher.Hash(request.Password), _clock.UtcNow);

        await _users.AddAsync(user, cancellationToken);

        return Issue(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!Email.IsValid(request.Email))
        {
            throw new InvalidCredentialsException();
        }

        var user = await _users.GetByEmailAsync(Email.Create(request.Email), cancellationToken);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return Issue(user);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

        return ToDto(user);
    }

    private AuthResult Issue(User user)
    {
        var token = _tokens.Generate(user);

        return new AuthResult(token.Value, token.ExpiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Email.Value, user.DisplayName, user.CreatedAt);
}
