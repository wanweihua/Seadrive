using System.Data.Entity;
using Data_Abstraction_Layer.Transport.Models;

namespace Data_Abstraction_Layer.Transport
{
    public class TransportContext : DbContext
    {
        public TransportContext()
            : base("name=transportEntities")
        {
            Database.SetInitializer<TransportContext>(null);
        }

        public DbSet<ChunksReceived> ChunksReceived { get; set; }
        public DbSet<LocalServerIdSession> LocalServerIdSession { get; set; }
        public DbSet<VirtualFile> VirtualFile { get; set; } 
    }
}
