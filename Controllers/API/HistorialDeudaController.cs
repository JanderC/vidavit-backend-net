using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    /// <summary>
    /// API: Historial completo de una deuda — abonos, cambio de estado, pago desde esta vista.
    /// Rutas base: /api/HistorialDeuda
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HistorialDeudaController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HistorialDeudaController> _logger;

        public HistorialDeudaController(AppDbContext context, ILogger<HistorialDeudaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─────────────────────────────────────────────
        // HELPER: obtener primer usuario activo del sistema
        // ─────────────────────────────────────────────
        private async Task<Guid> ObtenerUsuarioSistemaAsync()
        {
            var usuario = await _context.Usuarios
                .Where(u => u.Activo)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (usuario == Guid.Empty)
                throw new Exception("No hay usuarios activos en el sistema");

            return usuario;
        }

        // ─────────────────────────────────────────────
        // HELPER: Calcular estado real (no confiar solo en BD)
        // ✅ FIX CRÍTICO: Una deuda "pendiente" que ya pasó su fecha
        //    se devuelve como "vencida" sin importar lo que diga la BD.
        // ─────────────────────────────────────────────
        private static string CalcularEstadoReal(DeudaCliente deuda)
        {
            if (deuda.Estado == "pagada" || deuda.Estado == "cancelada")
                return deuda.Estado;

            if (deuda.FechaVencimiento.HasValue &&
                deuda.FechaVencimiento.Value.Date < DateTime.Now.Date &&
                deuda.Saldo > 0)
            {
                return "vencida";
            }

            return deuda.Estado;
        }

        // ─────────────────────────────────────────────
        // GET /api/HistorialDeuda/{deudaId}
        // Devuelve la deuda completa + todos sus abonos ordenados cronológicamente
        // ─────────────────────────────────────────────
        [HttpGet("{deudaId}")]
        public async Task<IActionResult> GetHistorial(Guid deudaId)
        {
            try
            {
                var deuda = await _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .FirstOrDefaultAsync(d => d.Id == deudaId);

                if (deuda == null)
                    return NotFound(new { success = false, message = "Deuda no encontrada" });

                // ✅ FIX: Si en BD dice "pendiente" pero ya venció, actualizar automáticamente
                var estadoReal = CalcularEstadoReal(deuda);
                if (deuda.Estado != estadoReal)
                {
                    deuda.Estado = estadoReal;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("🔄 Deuda {DeudaId} actualizada automáticamente a estado: {Estado}", deudaId, estadoReal);
                }

                var abonos = await _context.AbonosDeuda
                    .Where(a => a.DeudaId == deudaId)
                    .OrderBy(a => a.FechaAbono)
                    .Select(a => new
                    {
                        a.Id,
                        a.Monto,
                        a.MetodoPago,
                        Descripcion = a.Notas,
                        fecha = a.FechaAbono.ToString("yyyy-MM-ddTHH:mm:ss")
                    })
                    .ToListAsync();

                // Calcular saldo progresivo para la línea de tiempo
                decimal saldoAcumulado = deuda.MontoTotal;
                var abonosConSaldo = abonos.Select(a =>
                {
                    saldoAcumulado -= a.Monto;
                    return new
                    {
                        a.Id,
                        a.Monto,
                        a.MetodoPago,
                        a.Descripcion,
                        a.fecha,
                        saldoTrasAbono = saldoAcumulado < 0 ? 0 : saldoAcumulado
                    };
                }).ToList();

                // ✅ FIX: Usar estado real calculado (no el que tenía en BD antes)
                var estaVencida = estadoReal == "vencida";
                var diasParaVencer = deuda.FechaVencimiento.HasValue
                    ? (int)(deuda.FechaVencimiento.Value.Date - DateTime.Now.Date).TotalDays
                    : (int?)null;

                return Ok(new
                {
                    success = true,
                    deuda = new
                    {
                        deuda.Id,
                        Estado = estadoReal,                     // ← siempre el real
                        EstadoBD = deuda.Estado,                 // ← el de BD (referencia)
                        deuda.Concepto,
                        deuda.MontoTotal,
                        deuda.MontoPagado,
                        deuda.Saldo,
                        fechaCreacion = deuda.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaVencimiento = deuda.FechaVencimiento.HasValue
                            ? deuda.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                            : (string?)null,
                        estaVencida,
                        diasParaVencer,
                        cliente = new
                        {
                            deuda.Cliente.Id,
                            nombre = $"{deuda.Cliente.Nombre} {deuda.Cliente.Apellido}".Trim(),
                            cedula = deuda.Cliente.Cedula
                        }
                    },
                    abonos = abonosConSaldo,
                    totalAbonos = abonos.Count,
                    totalPagado = abonos.Sum(a => a.Monto)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de deuda {DeudaId}", deudaId);
                return StatusCode(500, new { success = false, message = "Error al obtener historial", error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // POST /api/HistorialDeuda/abonar
        // Registra un abono desde la vista de historial
        // ─────────────────────────────────────────────
        [HttpPost("abonar")]
        public async Task<IActionResult> Abonar([FromBody] AbonarDesdeHistorialRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                var deuda = await _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .FirstOrDefaultAsync(d => d.Id == request.DeudaId);

                if (deuda == null)
                    return NotFound(new { success = false, message = "Deuda no encontrada" });

                if (deuda.Estado == "pagada")
                    return BadRequest(new { success = false, message = "Esta deuda ya está completamente pagada" });

                if (deuda.Estado == "cancelada")
                    return BadRequest(new { success = false, message = "Esta deuda fue cancelada y no admite abonos" });

                if (request.Monto > deuda.Saldo)
                    return BadRequest(new
                    {
                        success = false,
                        message = $"El monto (${request.Monto:N2}) supera el saldo pendiente (${deuda.Saldo:N2})"
                    });

                var usuarioId = await ObtenerUsuarioSistemaAsync();
                var metodoPago = request.MetodoPago ?? "efectivo";

                // Registrar en AbonosDeuda
                var abono = new AbonoDeuda
                {
                    Id = Guid.NewGuid(),
                    DeudaId = deuda.Id,
                    Monto = request.Monto,
                    MetodoPago = metodoPago,
                    FechaAbono = DateTime.Now,
                    Notas = string.IsNullOrWhiteSpace(request.Notas)
                        ? $"Abono desde historial — {deuda.Concepto}"
                        : request.Notas
                };
                _context.AbonosDeuda.Add(abono);

                // Registrar en MovimientosCaja
                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = "abono_deuda",
                    MetodoPago = metodoPago,
                    Monto = request.Monto,
                    Descripcion = $"Abono deuda — {deuda.Cliente.Nombre} {deuda.Cliente.Apellido} — {deuda.Concepto}",
                    ReferenciaId = deuda.Id,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CierreCajaId = null,
                    MovimientoCajaFuerteId = null,
                    CreatedAt = DateTime.Now
                };
                _context.MovimientosCaja.Add(movimiento);

                // Actualizar saldo de la deuda
                deuda.MontoPagado += request.Monto;
                deuda.Saldo = deuda.MontoTotal - deuda.MontoPagado;

                if (deuda.Saldo <= 0)
                {
                    deuda.Saldo = 0;
                    deuda.Estado = "pagada";
                    _logger.LogInformation("✅ Deuda {DeudaId} marcada como PAGADA desde historial", deuda.Id);
                }
                else
                {
                    // ✅ FIX: Recalcular estado real después del abono
                    var estadoReal = CalcularEstadoReal(deuda);
                    deuda.Estado = estadoReal;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("💰 Abono desde historial: ${Monto} → Deuda {DeudaId}", request.Monto, deuda.Id);

                return Ok(new
                {
                    success = true,
                    message = deuda.Estado == "pagada"
                        ? $"Deuda pagada completamente con ${request.Monto:N2}"
                        : $"Abono de ${request.Monto:N2} registrado. Saldo restante: ${deuda.Saldo:N2}",
                    estadoDeuda = deuda.Estado,
                    saldoRestante = deuda.Saldo,
                    montoPagado = deuda.MontoPagado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar abono desde historial");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al registrar abono",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // ─────────────────────────────────────────────
        // PATCH /api/HistorialDeuda/{deudaId}/estado
        // Cambia el estado de la deuda manualmente
        // ─────────────────────────────────────────────
        [HttpPatch("{deudaId}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid deudaId, [FromBody] CambiarEstadoRequest request)
        {
            try
            {
                var estadosValidos = new[] { "pendiente", "vencida", "pagada", "cancelada" };

                if (string.IsNullOrWhiteSpace(request.NuevoEstado) || !estadosValidos.Contains(request.NuevoEstado))
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Estado inválido. Los válidos son: {string.Join(", ", estadosValidos)}"
                    });

                var deuda = await _context.DeudasClientes.FindAsync(deudaId);
                if (deuda == null)
                    return NotFound(new { success = false, message = "Deuda no encontrada" });

                if (deuda.Estado == "pagada" && request.NuevoEstado != "cancelada")
                    return BadRequest(new { success = false, message = "No se puede cambiar el estado de una deuda ya pagada" });

                var estadoAnterior = deuda.Estado;
                deuda.Estado = request.NuevoEstado;

                await _context.SaveChangesAsync();

                _logger.LogInformation("🔄 Estado deuda {DeudaId}: {Anterior} → {Nuevo} (motivo: {Motivo})",
                    deudaId, estadoAnterior, request.NuevoEstado, request.Motivo ?? "sin motivo");

                return Ok(new
                {
                    success = true,
                    message = $"Estado actualizado: {estadoAnterior} → {request.NuevoEstado}",
                    estadoAnterior,
                    estadoNuevo = request.NuevoEstado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de deuda {DeudaId}", deudaId);
                return StatusCode(500, new { success = false, message = "Error al cambiar estado", error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // MODELOS DE REQUEST
        // ─────────────────────────────────────────────

        public class AbonarDesdeHistorialRequest
        {
            public Guid DeudaId { get; set; }
            public decimal Monto { get; set; }
            public string? MetodoPago { get; set; }
            public string? Notas { get; set; }
        }

        public class CambiarEstadoRequest
        {
            public string NuevoEstado { get; set; } = string.Empty;
            public string? Motivo { get; set; }
        }
    }
}
