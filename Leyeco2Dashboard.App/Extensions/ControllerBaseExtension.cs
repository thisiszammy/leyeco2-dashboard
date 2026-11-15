using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Leyeco2Dashboard.App.Extensions
{
    public static class ControllerBaseExtension
    {
        public static string? GetUserId(this ControllerBase controller)
        {
            return controller.User.FindFirstValue("sub");
        }

        public static Guid? GetUserGuid(this ControllerBase controller)
        {
            var userId = controller.GetUserId();

            return Guid.TryParse(userId, out var guid)
                ? guid
                : null;
        }

        public static Guid GetRequiredUserGuid(this ControllerBase controller)
        {
            var userId = controller.GetUserId();

            if (!Guid.TryParse(userId, out var guid))
                throw new UnauthorizedAccessException("User ID claim is missing or invalid.");

            return guid;
        }
    }
}
