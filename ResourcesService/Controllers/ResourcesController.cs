using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResourcesService.Data;
using ResourcesService.Models;
using System.Net.Http;

namespace ResourcesService.Controllers
{
	public class CreateResourceRequest
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string? Type { get; set; }
		public decimal? Price { get; set; }
		public int? Capacity { get; set; }
		public string? Location { get; set; }
		public bool IsAvailable { get; set; } = true;
		public string? Features { get; set; }
	}

	public class UpdateResourceRequest
	{
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Type { get; set; }
		public decimal? Price { get; set; }
		public int? Capacity { get; set; }
		public string? Location { get; set; }
		public bool? IsAvailable { get; set; }
		public string? Features { get; set; }
	}

	[ApiController]
	[Route("api/resources")]
	public class ResourcesController : ControllerBase
	{
		private readonly ResourceDbContext _context;
		private readonly IHttpClientFactory _httpClientFactory;

		public ResourcesController(ResourceDbContext context, IHttpClientFactory httpClientFactory)
		{
			_context = context;
			_httpClientFactory = httpClientFactory;
		}

		[HttpGet]
		public async Task<IActionResult> GetAll()
		{
			try
			{
				var resources = await _context.Resources
					.OrderByDescending(r => r.CreatedAt)
					.ToListAsync();

				return Ok(resources);
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while retrieving resources." });
			}
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> GetById(int id)
		{
			try
			{
				var resource = await _context.Resources.FindAsync(id);

				if (resource == null)
				{
					return NotFound(new { Message = $"Resource with ID {id} not found." });
				}

				return Ok(resource);
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while retrieving the resource." });
			}
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateResourceRequest request)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(request.Name))
				{
					return BadRequest(new { Message = "Name is required." });
				}

				if (string.IsNullOrWhiteSpace(request.Description))
				{
					return BadRequest(new { Message = "Description is required." });
				}

				var existingResource = await _context.Resources
					.FirstOrDefaultAsync(r =>
						r.Name == request.Name &&
						r.Location == request.Location
					);

				if (existingResource != null)
				{
					return Conflict(new { Message = $"Resource '{request.Name}' at location '{request.Location}' already exists." });
				}

				var resource = new Resource
				{
					Name = request.Name,
					Description = request.Description,
					Type = request.Type,
					Price = request.Price,
					Capacity = request.Capacity,
					Location = request.Location,
					IsAvailable = request.IsAvailable,
					Features = request.Features,
					CreatedAt = DateTime.UtcNow
				};

				_context.Resources.Add(resource);
				await _context.SaveChangesAsync();

				return CreatedAtAction(
					nameof(GetById),
					new { id = resource.Id },
					resource
				);
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while creating the resource." });
			}
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> Update(int id, [FromBody] UpdateResourceRequest request)
		{
			try
			{
				var resource = await _context.Resources.FindAsync(id);

				if (resource == null)
				{
					return NotFound(new { Message = $"Resource with ID {id} not found." });
				}

				if (!string.IsNullOrEmpty(request.Name))
					resource.Name = request.Name;
				if (!string.IsNullOrEmpty(request.Description))
					resource.Description = request.Description;
				if (request.Type != null)
					resource.Type = request.Type;
				if (request.Price.HasValue)
					resource.Price = request.Price;
				if (request.Capacity.HasValue)
					resource.Capacity = request.Capacity;
				if (request.Location != null)
					resource.Location = request.Location;
				if (request.IsAvailable.HasValue)
					resource.IsAvailable = request.IsAvailable.Value;
				if (request.Features != null)
					resource.Features = request.Features;

				await _context.SaveChangesAsync();

				return Ok(resource);
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while updating the resource." });
			}
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var resource = await _context.Resources.FindAsync(id);

				if (resource == null)
				{
					return NotFound(new { Message = $"Resource with ID {id} not found." });
				}

				_context.Resources.Remove(resource);
				await _context.SaveChangesAsync();

				return Ok(new { Message = $"Resource with ID {id} deleted successfully." });
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while deleting the resource." });
			}
		}
	}
}