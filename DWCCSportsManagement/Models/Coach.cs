using System.ComponentModel.DataAnnotations;

namespace DWCCSportsManagement.Models
{
    public class Coach
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string Phone { get; set; }

        public string ImageUrl { get; set; }
        public string Bio { get; set; }
        public string Status { get; set; }

        public List<Team> Teams { get; set; } = new List<Team>();
    }
}
