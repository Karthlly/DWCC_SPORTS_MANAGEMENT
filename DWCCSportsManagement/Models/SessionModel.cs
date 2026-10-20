using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    public class SessionModel
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public DateTime DateTime { get; set; }

        // Foreign key to Team
        [Required]
        [ForeignKey("Team")]
        public int TeamId { get; set; }

        // Navigation property (nullable)
        public TeamModel? Team { get; set; }

        public string Description { get; set; }

        [MaxLength(150)]
        public string Location { get; set; }

        public String Status { get; set; } = "Active";
    }
}
