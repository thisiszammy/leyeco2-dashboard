using Leyeco2Dashboard.Data.Entities.Database;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IUserSessionRepository
    {
        DbSet<UserSession> GetUserSessions();
        void AddUserSession(UserSession userSession);
        void UpdateUserSession(UserSession userSession);
    }
}
