
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Abstraction_Layer.Transport.Models
{
    [Serializable]
    [Table("VirtualFile")]
    public class VirtualFile
    {
        [Key]
        public string Guid { get; set; }
        public string RelativePath { get; set; }
        public byte[] FileChecksum { get; set; }
    }
}
