using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembresiasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembresiasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Membresias
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresias()
        {
            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        // GET: api/Membresias/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Membresia>> GetMembresia(Guid id)
        {
            var membresia = await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (membresia == null)
                return NotFound();

            return membresia;
        }

        // POST: api/Membresias
        [HttpPost]
        public async Task<ActionResult<Membresia>> CreateMembresia([FromBody] Membresia membresia)
        {
            membresia.Id = Guid.NewGuid();
            membresia.CreatedAt = DateTime.UtcNow;
            membresia.UpdatedAt = DateTime.UtcNow;
            _context.Membresias.Add(membresia);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMembresia), new { id = membresia.Id }, membresia);
        }

        // PUT: api/Membresias/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMembresia(Guid id, [FromBody] Membresia membresia)
        {
            if (id != membresia.Id)
                return BadRequest();

            var dbMembresia = await _context.Membresias.FindAsync(id);
            if (dbMembresia == null)
                return NotFound();

            dbMembresia.ClienteId = membresia.ClienteId;
            dbMembresia.PlanId = membresia.PlanId;
            dbMembresia.FechaInicio = membresia.FechaInicio;
            dbMembresia.FechaVencimiento = membresia.FechaVencimiento;
            dbMembresia.Estado = membresia.Estado;
            dbMembresia.MontoPagado = membresia.MontoPagado;
            dbMembresia.MetodoPago = membresia.MetodoPago;
            dbMembresia.Notas = membresia.Notas;
            dbMembresia.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Membresias/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMembresia(Guid id)
        {
            var membresia = await _context.Membresias.FindAsync(id);
            if (membresia == null)
                return NotFound();

            _context.Membresias.Remove(membresia);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}