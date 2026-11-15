using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Repositories
{
    public class ActivityLogRepository : BaseRepository<DatabaseContext>, IActivityLogRepository
    {
        public ActivityLogRepository(UnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public DbSet<ActivityLog> GetActivityLogs()
        {
            return GetDbSet<ActivityLog>();
        }

        public void AddActivityLog(ActivityLog activityLog)
        {
            GetDbSet<ActivityLog>().Add(activityLog);
        }
    }
}
