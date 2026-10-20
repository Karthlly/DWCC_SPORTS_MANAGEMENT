    namespace DWCCSportsManagement.Models
    {
        public class Athlete
        {
            public int AthleteID { get; set; }
            public string Position { get; set; }
            public string AthleteName { get; set; }
            public int Age { get; set; }
            public string Status { get; set; }
            public string TeamName { get; set; }
            public string SportName { get; set; } // <-- Add this property to fix CS1061
        }
    }
