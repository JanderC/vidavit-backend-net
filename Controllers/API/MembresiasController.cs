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

        /// <summary>
        /// Calcula la fecha de vencimiento según el tipo de plan
        /// </summary>
        /// <param name="fechaInicio">Fecha de inicio (debe ser solo fecha, sin hora)</param>
        /// <param name="plan">Plan con la configuración de duración</param>
        /// <returns>Fecha de vencimiento (incluye el último día)</returns>
        private DateTime CalcularFechaVencimiento(DateTime fechaInicio, Plan plan)
        {
            var inicio = fechaInicio.Date;
            DateTime vencimiento;

            switch (plan.TipoCalculoVencimiento.ToLower())
            {
                case "meses":
                    // Sumar meses calendarios (ejemplo: 5 Feb -> 5 Mar)
                    vencimiento = inicio.AddMonths(plan.CantidadUnidades);
                    break;

                case "semanas":
                    // Sumar semanas (ejemplo: Lunes -> Lunes siguiente)
                    vencimiento = inicio.AddDays(plan.CantidadUnidades * 7);
                    break;

                case "anios":
                    // Sumar años (ejemplo: 5 Feb 2024 -> 5 Feb 2025)
                    vencimiento = inicio.AddYears(plan.CantidadUnidades);
                    break;

                case "dias":
                default:
                    // Sumar días corridos
                    vencimiento = inicio.AddDays(plan.CantidadUnidades - 1);
                    break;
            }

            return vencimiento.Date;
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
            var hoy = DateTime.Now.Date;

            var membresiasVencidas = await _context.Membresias
                .Where(m => m.Estado == "activa" && m.FechaVencimiento < hoy)
                .ToListAsync();

            if (membresiasVencidas.Any())
            {
                foreach (var membresia in membresiasVencidas)
                {
                    membresia.Estado = "vencida";
                    membresia.UpdatedAt = DateTime.Now;
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

                var fechaInicioStr = data.GetProperty("fechaInicio").GetString();
                var fechaInicio = DateTime.Parse(fechaInicioStr).Date;

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

                // Calcular fecha de vencimiento
                var fechaVencimiento = CalcularFechaVencimiento(fechaInicio, plan);

                // Crear membresía
                var membresia = new Membresia
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    PlanId = planId,
                    FechaInicio = fechaInicio,
                    FechaVencimiento = fechaVencimiento,
                    Estado = "activa",
                    MontoPagado = montoPagado,
                    MetodoPago = metodoPago,
                    Notas = notas,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Membresias.Add(membresia);

                // Registrar pago
                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    MembresiaId = membresia.Id,
                    Monto = montoPagado,
                    FechaPago = DateTime.Now,
                    MetodoPago = metodoPago,
                    ReciboNumero = null,
                    Notas = montoPagado < plan.Precio ? "Pago parcial de membresía" : null,
                    CreatedAt = DateTime.Now
                };

                _context.Pagos.Add(pago);

                // Manejar deuda si no se pagó completo
                Guid? deudaId = null;
                var saldoPendiente = plan.Precio - montoPagado;

                if (saldoPendiente > 0)
                {
                    var deuda = new DeudaCliente
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = clienteId,
                        Concepto = $"Pago parcial membresía {plan.Nombre}",
                        MontoTotal = plan.Precio,
                        MontoPagado = montoPagado,
                        Saldo = saldoPendiente,
                        Estado = "pendiente",
                        FechaCreacion = DateTime.Now,
                        FechaVencimiento = fechaVencimiento,
                        Notas = $"Membresía ID: {membresia.Id}",
                        CreatedAt = DateTime.Now
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
                        Fecha = DateTime.Now.Date,
                        CreatedAt = DateTime.Now
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

                // CORRECCIÓN: SIEMPRE renovar desde la fecha de vencimiento actual
                // Independientemente de si está vencida o no, la renovación debe partir
                // de la fecha de vencimiento para mantener la continuidad del periodo
                // Ejemplo: si vence el 7 de marzo y paga el 10 de marzo, renueva desde el 8 de marzo
                DateTime fechaInicio = membresia.FechaVencimiento; 

                var fechaVencimiento = CalcularFechaVencimiento(fechaInicio, plan);

                bool cambioDePlan = planId != membresia.PlanId;

                membresia.PlanId = planId;
                membresia.FechaInicio = fechaInicio;
                membresia.FechaVencimiento = fechaVencimiento;
                membresia.Estado = "activa";
                membresia.MontoPagado = montoPagado;
                membresia.MetodoPago = metodoPago;
                membresia.Notas = cambioDePlan ? $"Renovación con cambio a {plan.Nombre}" : (notas ?? "Renovación");
                membresia.UpdatedAt = DateTime.Now;

                // Registrar pago
                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    ClienteId = membresia.ClienteId,
                    MembresiaId = membresia.Id,
                    Monto = montoPagado,
                    FechaPago = DateTime.Now,
                    MetodoPago = metodoPago,
                    ReciboNumero = null,
                    Notas = "Renovación",
                    CreatedAt = DateTime.Now
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
                    Fecha = DateTime.Now.Date,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();

                // Mensaje mejorado con información del tipo de plan
                string tipoRenovacion = plan.TipoCalculoVencimiento.ToLower() switch
                {
                    "meses" => $"{plan.CantidadUnidades} {(plan.CantidadUnidades == 1 ? "mes" : "meses")}",
                    "semanas" => $"{plan.CantidadUnidades} {(plan.CantidadUnidades == 1 ? "semana" : "semanas")}",
                    "anios" => $"{plan.CantidadUnidades} {(plan.CantidadUnidades == 1 ? "año" : "años")}",
                    _ => $"{plan.DuracionDias} días"
                };

                return Ok(new
                {
                    success = true,
                    data = membresia,
                    message = $"Membresía renovada por {tipoRenovacion} (vence el {fechaVencimiento:dd/MM/yyyy})"
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