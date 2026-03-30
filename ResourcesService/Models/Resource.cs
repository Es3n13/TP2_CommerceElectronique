using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResourcesService.Models
{
	[Table("Resources")]
	public class Resource
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		public string Name { get; set; } = string.Empty;

		[Required]
		public string Description { get; set; } = string.Empty;

		public string? Type { get; set; }

		public decimal? Price { get; set; }

		public int? Capacity { get; set; }

		public string? Location { get; set; }

		public bool IsAvailable { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public string? Features { get; set; }
	}
}