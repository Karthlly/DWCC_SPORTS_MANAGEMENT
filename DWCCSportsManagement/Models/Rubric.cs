namespace DWCCSportsManagement.Models
{
    public class Rubric
    {
        public int Id { get; set; }
        public string Criteria { get; set; }
        public int Points { get; set; }        // <-- numeric
        public string Description { get; set; } // <-- the text from each cell
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

}
