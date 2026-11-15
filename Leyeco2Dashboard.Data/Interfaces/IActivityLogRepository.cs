using Leyeco2Dashboard.Data.Entities.Database;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IActivityLogRepository
    {
        DbSet<ActivityLog> GetActivityLogs();
        void AddActivityLog(ActivityLog activityLog);
    }
}
