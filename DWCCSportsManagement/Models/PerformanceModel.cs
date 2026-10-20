using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    [Table("performance")]
    public class PerformanceModel
    {
        public int Id { get; set; }
        public int AthleteId { get; set; }
        public int SessionId { get; set; }
        public string Remarks { get; set; }
        public int Rating { get; set; }
        public int MaxRating { get; set; }

        public int? ScholarshipPercentage { get; set; }

    }
}
