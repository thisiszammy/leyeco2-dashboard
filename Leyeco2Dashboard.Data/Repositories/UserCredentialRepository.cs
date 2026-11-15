using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Repositories
{
    public class UserCredentialRepository : BaseRepository<DatabaseContext>, IUserCredentialRepository
    {
        public UserCredentialRepository(UnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public DbSet<UserCredential> GetUserCredentials()
        {
            return GetDbSet<UserCredential>();
        }

        public void AddUserCredential(UserCredential userCredential)
        {
            GetDbSet<UserCredential>().Add(userCredential);
        }

        public void UpdateUserCredential(UserCredential userCredential)
        {
            GetDbSet<UserCredential>().Update(userCredential);
        }
    }
}
