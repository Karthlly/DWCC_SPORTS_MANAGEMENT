using System.ComponentModel.DataAnnotations.Schema;

namespace DWCCSportsManagement.Models
{
    public class Sport
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; } = "Active";

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("sub_category")]
        public int SubCategoryId { get; set; } // FK to SubCategory.Id

        public Category Category { get; set; }


        public SubCategory SubCategory { get; set; } // navigation property
    }
}
