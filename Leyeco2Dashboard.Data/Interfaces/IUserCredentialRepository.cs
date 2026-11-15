using Leyeco2Dashboard.Data.Entities.Database;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IUserCredentialRepository
    {
        DbSet<UserCredential> GetUserCredentials();
        void AddUserCredential(UserCredential userCredential);
        void UpdateUserCredential(UserCredential userCredential);
    }
}
