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
            // Actualizar estados automáticamente antes de devolver
            await ActualizarEstadosVencidas();

            return await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        private async Task ActualizarEstadosVencidas()
        {
            var hoy = DateTime.UtcNow.Date;
            var membresiasVencidas = await _context.Membresias
                .Where(m => m.Estado == "activa" && m.FechaVencimiento < hoy)
                .ToListAsync();

            if (membresiasVencidas.Any())
            {
                foreach (var membresia in membresiasVencidas)
                {
                    membresia.Estado = "vencida";
                    membresia.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }
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

                // Obtener usuario ID si está disponible (para movimientos de caja)
                Guid? usuarioId = null;
                if (data.TryGetProperty("usuarioId", out var userEl) && !string.IsNullOrWhiteSpace(userEl.GetString()))
                {
                    usuarioId = Guid.Parse(userEl.GetString());
                }

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

                // Validar que el monto pagado no sea mayor al precio del plan
                if (montoPagado > plan.Precio)
                {
                    return Ok(new { success = false, message = "El monto pagado no puede ser mayor al precio del plan" });
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
                    Notas = montoPagado < plan.Precio ? "Pago parcial de membresía" : null,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Pagos.Add(pago);

                // SI HAY DEUDA (pago parcial), registrarla automáticamente
                decimal saldoPendiente = plan.Precio - montoPagado;
                Guid? deudaId = null;

                if (saldoPendiente > 0)
                {
                    var deuda = new DeudaCliente
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = clienteId,
                        Concepto = $"Saldo pendiente membresía {plan.Nombre}",
                        MontoTotal = saldoPendiente,
                        MontoPagado = 0,
                        Saldo = saldoPendiente,
                        Estado = "pendiente",
                        FechaCreacion = DateTime.UtcNow,
                        FechaVencimiento = DateTime.SpecifyKind(fechaVencimiento, DateTimeKind.Utc),
                        Notas = $"Pago inicial: ${montoPagado:N2} de ${plan.Precio:N2}",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.DeudasClientes.Add(deuda);
                    deudaId = deuda.Id;
                }

                // Registrar ingreso en caja (solo el monto pagado)
                if (usuarioId.HasValue && montoPagado > 0)
                {
                    var movimientoCaja = new MovimientoCaja
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Categoria = "membresia",
                        Monto = montoPagado,
                        Descripcion = $"Venta membresía {plan.Nombre} - {cliente.Nombre} {cliente.Apellido}" +
                                    (saldoPendiente > 0 ? $" (Pago parcial, saldo: ${saldoPendiente:N2})" : ""),
                        ReferenciaId = membresia.Id,
                        UsuarioId = usuarioId.Value,
                        MetodoPago = metodoPago,
                        Fecha = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.MovimientosCaja.Add(movimientoCaja);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = membresia,
                    deudaId = deudaId,
                    saldoPendiente = saldoPendiente,
                    message = saldoPendiente > 0
                        ? $"Membresía creada. Saldo pendiente: ${saldoPendiente:N2}"
                        : "Membresía creada exitosamente"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al crear membresía",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
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
                    PlanPrecio = m.Plan.Precio,
                    FechaInicio = m.FechaInicio,
                    FechaVencimiento = m.FechaVencimiento,
                    Estado = m.Estado,
                    MontoPagado = m.MontoPagado,
                    MetodoPago = m.MetodoPago,
                    SaldoPendiente = m.Plan.Precio - m.MontoPagado
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