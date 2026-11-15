using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Repositories
{
    public class UserRepository : BaseRepository<DatabaseContext>, IUserRepository
    {
        public UserRepository(UnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public DbSet<User> GetUsers()
        {
            return GetDbSet<User>();
        }

        public void AddUser(User user)
        {
            GetDbSet<User>().Add(user);
        }

        public void UpdateUser(User user)
        {
            GetDbSet<User>().Update(user);
        }
    }
}
