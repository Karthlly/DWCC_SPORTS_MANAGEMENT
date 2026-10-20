using System;
using System.ComponentModel.DataAnnotations;

namespace DWCCSportsManagement.Models
{
    public class AthleteModel
    {
        public int AthleteId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public int StudentId { get; set; }

        public string Course { get; set; }

        public int YearLevel { get; set; }
        public string Gender { get; set; }
        public DateTime? Birthdate { get; set; }

        [Required]
        public string Status { get; set; } = "Active";  // <-- default value
        public string? MedicalInfo { get; set; }
        public string? Grades { get; set; }


        // Foreign keys
        public int? TeamId { get; set; }
        public int? SportId { get; set; }



        // Navigation properties
        public TeamModel? Team { get; set; }
        public Sport? Sport { get; set; }

        // For display
        public string? TeamName => Team?.TeamName;
        public string? SportName => Sport?.Name;
    }

}
