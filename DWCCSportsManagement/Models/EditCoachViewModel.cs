namespace DWCCSportsManagement.Models
{
    using Microsoft.AspNetCore.Http;
    using System.ComponentModel.DataAnnotations;

    public class EditCoachViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string Phone { get; set; }
        public string Bio { get; set; }
        public string Status { get; set; } = "Active";

        public IFormFile? ImageFile { get; set; } // optional file upload
    }

}
