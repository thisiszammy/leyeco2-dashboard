using Leyeco2Dashboard.Data.Entities.Database;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IUserGrantRepository
    {
        DbSet<UserGrant> GetUserGrants();
        void AddUserGrant(UserGrant userGrant);
        void UpdateUserGrant(UserGrant userGrant);
        void RemoveUserGrant(UserGrant userGrant);
    }
}
