using System.Data.Entity;
using Data_Abstraction_Layer.Deduplication.Models;

namespace Data_Abstraction_Layer.Deduplication
{
    public class DeduplicationContext : DbContext
    {
        public DeduplicationContext()
            : base("name=seadriveEntities")
        {
            Database.SetInitializer<DeduplicationContext>(null);
        }

        public DbSet<CacheEntries> CacheEntries { get; set; }
        public DbSet<FileDirectory> FileDirectory { get; set; }
    }
}
