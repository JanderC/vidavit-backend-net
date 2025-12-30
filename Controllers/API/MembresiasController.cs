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

                // Registrar ingreso en caja SIEMPRE que haya pago (aunque sea parcial)
                if (montoPagado > 0)
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
                        UsuarioId = usuarioId ?? Guid.Parse("00000000-0000-0000-0000-000000000000"),
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
                    error = ex.Message
                });
            }
        }

        [HttpPost("renovar/{id}")]
        public async Task<IActionResult> RenovarMembresia(Guid id, [FromBody] JsonElement data)
        {
            try
            {
                var membresia = await _context.Membresias
                    .Include(m => m.Cliente)
                    .Include(m => m.Plan)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (membresia == null)
                    return Ok(new { success = false, message = "Membresía no encontrada" });

                var montoPagado = data.GetProperty("montoPagado").GetDecimal();
                var metodoPago = data.GetProperty("metodoPago").GetString();
                var notas = data.TryGetProperty("notas", out var notasEl) && !string.IsNullOrWhiteSpace(notasEl.GetString())
                    ? notasEl.GetString()
                    : null;

                Guid? usuarioId = null;
                if (data.TryGetProperty("usuarioId", out var userEl) && !string.IsNullOrWhiteSpace(userEl.GetString()))
                {
                    usuarioId = Guid.Parse(userEl.GetString());
                }

                // Determinar el plan a usar (nuevo o actual)
                Guid planId = membresia.PlanId;
                if (data.TryGetProperty("planId", out var planEl) && !string.IsNullOrWhiteSpace(planEl.GetString()))
                {
                    planId = Guid.Parse(planEl.GetString());
                }

                var plan = await _context.Planes.FindAsync(planId);
                if (plan == null || !plan.Activo)
                    return Ok(new { success = false, message = "Plan no válido" });

                if (montoPagado <= 0)
                    return Ok(new { success = false, message = "El monto debe ser mayor a 0" });

                // Validar que pague el precio completo del plan
                if (montoPagado != plan.Precio)
                    return Ok(new { success = false, message = $"Debe pagar el precio completo del plan: ${plan.Precio:N2}" });

                // Renovar membresía
                var duracion = plan.DuracionDias > 0 ? plan.DuracionDias : 30;
                bool cambioDePlan = planId != membresia.PlanId;

                membresia.PlanId = planId;
                membresia.FechaInicio = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                membresia.FechaVencimiento = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(duracion), DateTimeKind.Utc);
                membresia.Estado = "activa";
                membresia.MontoPagado = montoPagado;
                membresia.MetodoPago = metodoPago;
                membresia.Notas = cambioDePlan ? $"Renovación con cambio a {plan.Nombre}" : (notas ?? "Renovación");
                membresia.UpdatedAt = DateTime.UtcNow;

                // Registrar pago
                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    ClienteId = membresia.ClienteId,
                    MembresiaId = membresia.Id,
                    Monto = montoPagado,
                    FechaPago = DateTime.UtcNow,
                    MetodoPago = metodoPago,
                    ReciboNumero = null,
                    Notas = "Renovación",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Pagos.Add(pago);

                // Registrar movimiento de caja SIEMPRE
                var movimientoCaja = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = "renovacion",
                    Monto = montoPagado,
                    Descripcion = $"Renovación {plan.Nombre} - {membresia.Cliente.Nombre} {membresia.Cliente.Apellido}",
                    ReferenciaId = membresia.Id,
                    UsuarioId = usuarioId ?? Guid.Parse("00000000-0000-0000-0000-000000000000"),
                    MetodoPago = metodoPago,
                    Fecha = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = membresia,
                    message = $"Membresía renovada por {duracion} días"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al renovar membresía",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMembresia(Guid id)
        {
            try
            {
                var membresia = await _context.Membresias.FindAsync(id);
                if (membresia == null)
                    return NotFound();

                _context.Membresias.Remove(membresia);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error al eliminar membresía", error = ex.Message });
            }
        }
    }
}