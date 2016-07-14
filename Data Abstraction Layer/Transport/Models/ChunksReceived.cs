using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Abstraction_Layer.Transport.Models
{
    [Serializable]
    [Table("ChunksReceived")]
    public class ChunksReceived
    {
        public string LocalServerId { get; set; }
        public byte[] FileChecksum { get; set; }
        public int ChunkNum { get; set; }
        public int Size { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
    }
}
