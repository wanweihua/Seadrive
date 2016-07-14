using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Abstraction_Layer.Deduplication.Models
{
    [Serializable]
    [Table("CacheEntries")]
    public class CacheEntries
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public byte[] FileChecksum { get; set; }
        public int BlockNum { get; set; }
        public byte[] ZippedData { get; set; }
        public byte[] BlockChecksum { get; set; }
    }
}
