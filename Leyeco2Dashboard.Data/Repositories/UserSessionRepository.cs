using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Repositories
{
    public class UserSessionRepository : BaseRepository<DatabaseContext>, IUserSessionRepository
    {
        public UserSessionRepository(UnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public DbSet<UserSession> GetUserSessions()
        {
            return GetDbSet<UserSession>();
        }

        public void AddUserSession(UserSession userSession)
        {
            GetDbSet<UserSession>().Add(userSession);
        }

        public void UpdateUserSession(UserSession userSession)
        {
            GetDbSet<UserSession>().Update(userSession);
        }
    }
}
