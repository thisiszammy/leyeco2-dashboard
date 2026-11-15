using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Entities.Leyeco2OTF
{
    // Hand-written beside the scaffold (re-scaffolding leaves it alone). The report rows (Data/Reports) are read off
    // raw SQL and never written, so a decimal that no column types is given the widest a sum can be, and EF has no
    // truncation to warn about. The facility's own columns keep the types the scaffold gives them.
    public partial class DatabaseContext
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.Properties<decimal>().HavePrecision(38, 6);
    }
}
