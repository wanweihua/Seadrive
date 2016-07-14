using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Abstraction_Layer.Transport.Models
{
    [Serializable]
    [Table("localServerIdSession")]
    public class LocalServerIdSession
    {
        [Key]
        public string LocalServerId { get; set; }
        public byte[] CurrentFileInTransit { get; set; }
    }
}
