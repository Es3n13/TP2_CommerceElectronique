using Microsoft.AspNetCore.Mvc;

namespace ResourcesService.Controllers
{

    public class CreateResourceRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    [ApiController]
    [Route("api/resources")]
    public class ResourcesController : ControllerBase
    {
        // GET https://localhost:PORT/api/resources
        [HttpGet]
        public IActionResult GetAll()
        {

            var resources = new[]
            {
                new { Id = 1, Name = "Salle A",     Description = "Salle réunion 10 personnes" },
                new { Id = 2, Name = "Chambre 101", Description = "Chambre simple vue mer" }
            };

            return Ok(resources);
        }

        // POST https://localhost:PORT/api/resources
        [HttpPost]
        public IActionResult Create([FromBody] CreateResourceRequest request)
        {
            var createdResource = new
            {
                Id = 3, // Id simulé
                Name = request.Name,
                Description = request.Description
            };

            return Created(string.Empty, createdResource);
        }
    }
}
