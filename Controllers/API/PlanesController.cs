using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PlanesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Planes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Plan>>> GetPlanes()
        {
            return await _context.Planes.OrderBy(p => p.Nombre).ToListAsync();
        }

        // GET: api/Planes/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Plan>> GetPlan(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound();
            return plan;
        }

        // POST: api/Planes
        [HttpPost]
        public async Task<ActionResult<Plan>> CreatePlan([FromBody] Plan plan)
        {
            plan.Id = Guid.NewGuid();
            plan.CreatedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            _context.Planes.Add(plan);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }

        // PUT: api/Planes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] Plan plan)
        {
            if (id != plan.Id)
                return BadRequest();

            var planDb = await _context.Planes.FindAsync(id);
            if (planDb == null)
                return NotFound();

            // Actualiza solo los campos editables
            planDb.Nombre = plan.Nombre;
            planDb.Descripcion = plan.Descripcion;
            planDb.Tipo = plan.Tipo;
            planDb.DuracionDias = plan.DuracionDias;
            planDb.Precio = plan.Precio;
            planDb.Activo = plan.Activo;
            planDb.Color = plan.Color;
            planDb.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Planes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound();

            _context.Planes.Remove(plan);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}