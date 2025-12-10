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
        public async Task<ActionResult<Membresia>> CreateMembresia([FromBody] MembresiaDto dto)
        {
            try
            {
                // Validar cliente
                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null || !cliente.Activo)
                {
                    return BadRequest(new { success = false, message = "Cliente no válido" });
                }

                // Validar plan
                var plan = await _context.Planes.FindAsync(dto.PlanId);
                if (plan == null || !plan.Activo)
                {
                    return BadRequest(new { success = false, message = "Plan no válido" });
                }

                // Verificar membresía activa duplicada
                var tieneActiva = await _context.Membresias
                    .AnyAsync(m => m.ClienteId == dto.ClienteId && m.Estado == "activa");

                if (tieneActiva)
                {
                    return BadRequest(new { success = false, message = "El cliente ya tiene una membresía activa" });
                }

                // Crear membresía
                var membresia = new Membresia
                {
                    Id = Guid.NewGuid(),
                    ClienteId = dto.ClienteId,
                    PlanId = dto.PlanId,
                    FechaInicio = dto.FechaInicio,
                    FechaVencimiento = dto.FechaVencimiento,
                    Estado = dto.Estado ?? "activa",
                    MontoPagado = dto.MontoPagado,
                    MetodoPago = dto.MetodoPago,
                    Notas = string.IsNullOrWhiteSpace(dto.Notas) ? "ninguna" : dto.Notas,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Membresias.Add(membresia);

                // Registrar pago - SIN UpdatedAt porque no existe en el modelo
                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    ClienteId = membresia.ClienteId,
                    MembresiaId = membresia.Id,
                    Monto = dto.MontoPagado,
                    FechaPago = DateTime.UtcNow,
                    MetodoPago = dto.MetodoPago,
                    CreatedAt = DateTime.UtcNow
                    // NO incluir UpdatedAt - no existe en el modelo Pago
                };

                _context.Pagos.Add(pago);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetMembresia), new { id = membresia.Id }, membresia);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al crear membresía", error = ex.Message });
            }
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

    // DTO para crear membresía sin propiedades de navegación
    public class MembresiaDto
    {
        public Guid ClienteId { get; set; }
        public Guid PlanId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Estado { get; set; }
        public decimal MontoPagado { get; set; }
        public string MetodoPago { get; set; }
        public string Notas { get; set; }
    }
}