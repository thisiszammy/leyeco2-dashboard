using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Repositories
{
    public class UserGrantRepository : BaseRepository<DatabaseContext>, IUserGrantRepository
    {
        public UserGrantRepository(UnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public DbSet<UserGrant> GetUserGrants()
        {
            return GetDbSet<UserGrant>();
        }

        public void AddUserGrant(UserGrant userGrant)
        {
            GetDbSet<UserGrant>().Add(userGrant);
        }

        public void UpdateUserGrant(UserGrant userGrant)
        {
            GetDbSet<UserGrant>().Update(userGrant);
        }

        public void RemoveUserGrant(UserGrant userGrant)
        {
            GetDbSet<UserGrant>().Remove(userGrant);
        }
    }
}
