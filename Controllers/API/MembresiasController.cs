using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresias()
        {
            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

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

        [HttpPost]
        public async Task<IActionResult> CreateMembresia([FromBody] JsonElement data)
        {
            try
            {
                // Extraer datos del JSON
                var clienteId = Guid.Parse(data.GetProperty("clienteId").GetString());
                var planId = Guid.Parse(data.GetProperty("planId").GetString());
                var fechaInicio = DateTime.Parse(data.GetProperty("fechaInicio").GetString());
                var fechaVencimiento = DateTime.Parse(data.GetProperty("fechaVencimiento").GetString());
                var montoPagado = data.GetProperty("montoPagado").GetDecimal();
                var metodoPago = data.GetProperty("metodoPago").GetString();
                var notas = data.TryGetProperty("notas", out var notasEl) && !string.IsNullOrWhiteSpace(notasEl.GetString())
                    ? notasEl.GetString()
                    : null;

                // Validar cliente
                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null || !cliente.Activo)
                {
                    return Ok(new { success = false, message = "Cliente no válido" });
                }

                // Validar plan
                var plan = await _context.Planes.FindAsync(planId);
                if (plan == null || !plan.Activo)
                {
                    return Ok(new { success = false, message = "Plan no válido" });
                }

                // Crear membresía
                var membresia = new Membresia
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    PlanId = planId,
                    FechaInicio = DateTime.SpecifyKind(fechaInicio, DateTimeKind.Utc),
                    FechaVencimiento = DateTime.SpecifyKind(fechaVencimiento, DateTimeKind.Utc),
                    Estado = "activa",
                    MontoPagado = montoPagado,
                    MetodoPago = metodoPago,
                    Notas = notas,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Membresias.Add(membresia);

                // Registrar pago
                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    MembresiaId = membresia.Id,
                    Monto = montoPagado,
                    FechaPago = DateTime.UtcNow,
                    MetodoPago = metodoPago,
                    ReciboNumero = null,
                    Notas = null,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Pagos.Add(pago);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = membresia });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Error al crear membresía", error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

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

        [HttpGet("cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetMembresiasPorCliente(Guid clienteId)
        {
            var membresias = await _context.Membresias
                .Include(m => m.Plan)
                .Where(m => m.ClienteId == clienteId)
                .OrderByDescending(m => m.FechaInicio)
                .Select(m => new
                {
                    Id = m.Id,
                    PlanId = m.PlanId,
                    PlanNombre = m.Plan.Nombre,
                    FechaInicio = m.FechaInicio,
                    FechaVencimiento = m.FechaVencimiento,
                    Estado = m.Estado,
                    MontoPagado = m.MontoPagado,
                    MetodoPago = m.MetodoPago
                })
                .ToListAsync();

            return Ok(membresias);
        }

        [HttpGet("activas")]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresiasActivas()
        {
            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .Where(m => m.Estado == "activa")
                .OrderByDescending(m => m.FechaInicio)
                .ToListAsync();
        }

        [HttpGet("vencidas")]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresiasVencidas()
        {
            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .Where(m => m.Estado == "vencida")
                .OrderByDescending(m => m.FechaVencimiento)
                .ToListAsync();
        }

        [HttpGet("por-vencer")]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresiasPorVencer([FromQuery] int dias = 7)
        {
            var fechaLimite = DateTime.UtcNow.AddDays(dias);

            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .Where(m => m.Estado == "activa" && m.FechaVencimiento <= fechaLimite)
                .OrderBy(m => m.FechaVencimiento)
                .ToListAsync();
        }
    }
}