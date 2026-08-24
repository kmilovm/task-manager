using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using TaskManager.Application.Common;

namespace TaskManager.Api.Common;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidCredentialsException();
}
