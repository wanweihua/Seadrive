using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Abstraction_Layer.Deduplication.Models
{
    [Table("FileDirectory")]
    public class FileDirectory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Filename { get; set; }
        public bool IsDirectory { get; set; }
        public byte[] Checksum { get; set; }
        public bool IsDirty { get; set; }
    }
}
