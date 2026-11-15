using Leyeco2Dashboard.Authentication;
using Leyeco2Dashboard.App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Leyeco2Dashboard.App.Authorization
{
    /// <summary>
    /// Words a 403 on <c>/api</c> for the user: an <see cref="ApiResponse"/> naming the claim the refused action
    /// takes, which the console shows as it shows any refusal. The default forbid (the shared auth layer's JwtBearer
    /// scheme) answers with a status and no body, which read as the account having lost the Transaction Monitor when only one
    /// screen's grant was missing. Every other outcome is the default handler's.
    /// </summary>
    public class MissingGrantResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);

            if (!authorizeResult.Forbidden
                || !context.Request.Path.StartsWithSegments("/api")
                || context.Response.HasStarted)
            {
                return;
            }

            await context.Response.WriteAsJsonAsync(new ApiResponse
            {
                Code = StatusCodes.Status403Forbidden,
                Message = MessageFor(context, authorizeResult.AuthorizationFailure),
                Data = null
            });
        }

        private static string MessageFor(HttpContext context, AuthorizationFailure? failure)
        {
            var holdsAnyGrant = context.User
                .FindAll(AuthConstants.PermissionClaimType)
                .Any(claim => AuthConstants.TryParsePermission(claim.Value, out _));

            if (!holdsAnyGrant)
                return "This account has no access to the Transaction Monitor. Ask an administrator to give it access on the Users screen.";

            // All access meets every policy, so naming it beside the grant would only repeat that.
            var grants = (failure?.FailedRequirements ?? [])
                .OfType<ClaimsAuthorizationRequirement>()
                .Where(requirement => requirement.ClaimType == AuthConstants.PermissionClaimType)
                .SelectMany(requirement => requirement.AllowedValues ?? [])
                .Select(value => AuthConstants.TryParsePermission(value, out var permission) ? permission : (PermissionEnum?)null)
                .OfType<PermissionEnum>()
                .Where(permission => permission != AuthConstants.AllAccess)
                .Distinct()
                .Select(permission => "“" + permission.GetLabel() + "”")
                .ToList();

            return grants.Count switch
            {
                0 => "This account cannot do this in the Transaction Monitor.",
                1 => $"This needs the {grants[0]} permission, which this account does not hold. Ask an administrator to give it on the Users screen.",
                _ => $"This needs one of these permissions, which this account does not hold: {string.Join(" or ", grants)}. Ask an administrator to give one on the Users screen.",
            };
        }
    }
}
