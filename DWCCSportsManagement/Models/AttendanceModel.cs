using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    public enum AttendanceStatus
    {
        Present,
        Late,
        Absent, 
        Excused
    }

    public class AttendanceModel
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int AthleteId { get; set; }

        [ForeignKey("AthleteId")]
        public AthleteModel Athlete { get; set; }

        [Required]
        public int SessionId { get; set; }

        [ForeignKey("SessionId")]
        public SessionModel Session { get; set; }

        [Required]
        public AttendanceStatus Status { get; set; }
    }
}
