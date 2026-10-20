using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    public class TeamModel
    {
        internal object Athletes;

        [Key]
        public int TeamId { get; set; }

        [Required]
        public string TeamName { get; set; }

        [Required]
        [ForeignKey("Sport")]
        public int SportId { get; set; }

        [Required]
        [ForeignKey("Coach")]
        public int CoachId { get; set; } // keep property name CoachId

        public string Status { get; set; } = "Accepted";

        // Navigation properties
        public Sport? Sport { get; set; }

        [ForeignKey("CoachId")]
        public Coach? Coach { get; set; }
    }
}
