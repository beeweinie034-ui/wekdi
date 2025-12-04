using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wekdi.Models
{
    [Table("products")]
    public class Product
    {
        [Key]
        [Column("product_id")]
        public int ProductId { get; set; }

        [Required]
        [ForeignKey("Category")]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("price", TypeName = "decimal(6,2)")]
        public decimal Price { get; set; }

        [Column("stock")]
        public int Stock { get; set; } = 0;

        // New attribute for product image (store image filename or path)
        [StringLength(200)]
        [Column("image_path")]
        public string? ImagePath { get; set; }

        // Navigation property
        public Category? Category { get; set; }
    }
}
