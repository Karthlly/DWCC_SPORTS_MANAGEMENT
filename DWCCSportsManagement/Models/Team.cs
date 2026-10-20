namespace DWCCSportsManagement.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string TeamName { get; set; }
        public string Sport { get; set; }
        public int SportId { get; set; }  // <-- add this
        public int TeamId { get; set; }  // <-- add this
        public string Coach { get; set; }
        public string Status { get; set; }
    }
}
