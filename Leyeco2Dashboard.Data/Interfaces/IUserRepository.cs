using Leyeco2Dashboard.Data.Entities.Database;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IUserRepository
    {
        DbSet<User> GetUsers();
        void AddUser(User user);
        void UpdateUser(User user);
    }
}
