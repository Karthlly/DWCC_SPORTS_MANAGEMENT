using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    public class CompetitionParticipant
    {
        [Key]
        public int ParticipantId { get; set; }

        // Foreign key to Competition
        public int CompetitionId { get; set; }
        public Competition Competition { get; set; }

        // Foreign key to Team
        public int TeamId { get; set; }
        public TeamModel Team { get; set; }

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Results { get; set; } = "n/a"; // default value

        public String Status { get; set; } = "Processing";
    }
}
