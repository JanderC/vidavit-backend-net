using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    /// <summary>
    /// API: Historial de deudas de un cliente — todas sus deudas (activas y pagadas)
    /// y el detalle de abonos de cada una.
    ///
    /// Rutas:
    ///   GET  /api/HistorialDeuda/{deudaId}              → detalle de UNA deuda + sus abonos
    ///   GET  /api/HistorialDeuda/cliente/{clienteId}    → TODAS las deudas del cliente
    ///   POST /api/HistorialDeuda/abonar                 → registrar abono desde la vista de historial
    ///   PATCH /api/HistorialDeuda/{deudaId}/estado      → cambiar estado manualmente
    ///
    /// NOTA: El estado "vencida" fue eliminado. Solo existen: pendiente, pagada, cancelada.
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
        // GET /api/HistorialDeuda/{deudaId}
        // Devuelve una deuda completa + todos sus abonos en orden cronológico
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

                var diasParaVencer = deuda.FechaVencimiento.HasValue
                    ? (int)(deuda.FechaVencimiento.Value.Date - DateTime.Now.Date).TotalDays
                    : (int?)null;

                return Ok(new
                {
                    success = true,
                    deuda = new
                    {
                        deuda.Id,
                        Estado = deuda.Estado,
                        deuda.Concepto,
                        deuda.MontoTotal,
                        deuda.MontoPagado,
                        deuda.Saldo,
                        fechaCreacion = deuda.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaVencimiento = deuda.FechaVencimiento.HasValue
                            ? deuda.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                            : (string?)null,
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
        // GET /api/HistorialDeuda/pagadas
        // Devuelve todas las deudas pagadas de todos los clientes,
        // con búsqueda por nombre/cédula/concepto y paginación.
        // ─────────────────────────────────────────────
        [HttpGet("pagadas")]
        public async Task<IActionResult> GetDeudasPagadas(
            [FromQuery] string? busqueda = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int porPagina = 50)
        {
            try
            {
                var query = _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .Where(d => d.Estado == "pagada")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    var b = busqueda.ToLower();
                    query = query.Where(d =>
                        d.Cliente.Nombre.ToLower().Contains(b) ||
                        d.Cliente.Apellido.ToLower().Contains(b) ||
                        d.Cliente.Cedula.ToLower().Contains(b) ||
                        d.Concepto.ToLower().Contains(b));
                }

                var total = await query.CountAsync();
                var deudas = await query
                    .OrderByDescending(d => d.FechaCreacion)
                    .Skip((pagina - 1) * porPagina)
                    .Take(porPagina)
                    .ToListAsync();

                // Traer abonos de estas deudas
                var deudaIds = deudas.Select(d => d.Id).ToList();
                var todosAbonos = await _context.AbonosDeuda
                    .Where(a => deudaIds.Contains(a.DeudaId))
                    .OrderBy(a => a.FechaAbono)
                    .ToListAsync();
                var abonosPorDeuda = todosAbonos
                    .GroupBy(a => a.DeudaId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var items = deudas.Select(d =>
                {
                    var abonos = abonosPorDeuda.TryGetValue(d.Id, out var lista) ? lista : new();
                    decimal saldoAcum = d.MontoTotal;
                    var abonosConSaldo = abonos.Select(a =>
                    {
                        saldoAcum -= a.Monto;
                        return new
                        {
                            a.Id,
                            a.Monto,
                            a.MetodoPago,
                            descripcion = a.Notas,
                            fecha = a.FechaAbono.ToString("yyyy-MM-ddTHH:mm:ss"),
                            saldoTrasAbono = saldoAcum < 0 ? 0 : saldoAcum
                        };
                    }).ToList();

                    return new
                    {
                        d.Id,
                        d.Estado,
                        d.Concepto,
                        d.MontoTotal,
                        d.MontoPagado,
                        d.Saldo,
                        fechaCreacion = d.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaVencimiento = d.FechaVencimiento.HasValue
                            ? d.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                            : (string?)null,
                        cliente = new
                        {
                            d.Cliente.Id,
                            nombre = $"{d.Cliente.Nombre} {d.Cliente.Apellido}".Trim(),
                            cedula = d.Cliente.Cedula
                        },
                        abonos = abonosConSaldo,
                        totalAbonos = abonos.Count,
                        totalPagadoDeuda = abonos.Sum(a => a.Monto)
                    };
                }).ToList();

                return Ok(new
                {
                    success = true,
                    items,
                    total,
                    pagina,
                    porPagina,
                    totalPaginas = (int)Math.Ceiling((double)total / porPagina),
                    totalMonto = items.Sum(d => d.MontoTotal)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener deudas pagadas");
                return StatusCode(500, new { success = false, message = "Error al obtener deudas pagadas", error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // GET /api/HistorialDeuda/cliente/{clienteId}
        // Devuelve TODAS las deudas del cliente (pendientes + pagadas + canceladas)
        // junto con sus abonos y un resumen financiero global.
        // ─────────────────────────────────────────────
        [HttpGet("cliente/{clienteId}")]
        public async Task<IActionResult> GetHistorialCliente(Guid clienteId)
        {
            try
            {
                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null)
                    return NotFound(new { success = false, message = "Cliente no encontrado" });

                // Traer todas las deudas del cliente, más recientes primero
                var deudas = await _context.DeudasClientes
                    .Where(d => d.ClienteId == clienteId)
                    .OrderByDescending(d => d.FechaCreacion)
                    .ToListAsync();

                // Para cada deuda, traer sus abonos
                var deudaIds = deudas.Select(d => d.Id).ToList();

                var todosAbonos = await _context.AbonosDeuda
                    .Where(a => deudaIds.Contains(a.DeudaId))
                    .OrderBy(a => a.FechaAbono)
                    .ToListAsync();

                var abonosPorDeuda = todosAbonos
                    .GroupBy(a => a.DeudaId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var deudaItems = deudas.Select(d =>
                {
                    var abonos = abonosPorDeuda.TryGetValue(d.Id, out var lista) ? lista : new();

                    // Saldo progresivo para cada abono
                    decimal saldoAcum = d.MontoTotal;
                    var abonosConSaldo = abonos.Select(a =>
                    {
                        saldoAcum -= a.Monto;
                        return new
                        {
                            a.Id,
                            a.Monto,
                            a.MetodoPago,
                            descripcion = a.Notas,
                            fecha = a.FechaAbono.ToString("yyyy-MM-ddTHH:mm:ss"),
                            saldoTrasAbono = saldoAcum < 0 ? 0 : saldoAcum
                        };
                    }).ToList();

                    return new
                    {
                        d.Id,
                        d.Estado,
                        d.Concepto,
                        d.MontoTotal,
                        d.MontoPagado,
                        d.Saldo,
                        fechaCreacion = d.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaVencimiento = d.FechaVencimiento.HasValue
                            ? d.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                            : (string?)null,
                        abonos = abonosConSaldo,
                        totalAbonos = abonos.Count,
                        totalPagadoDeuda = abonos.Sum(a => a.Monto)
                    };
                }).ToList();

                // Resumen financiero del cliente
                var totalDeudas = deudas.Count;
                var deudas_pagadas = deudas.Count(d => d.Estado == "pagada");
                var deudas_activas = deudas.Count(d => d.Estado == "pendiente");
                var saldoPendiente = deudas.Where(d => d.Estado == "pendiente").Sum(d => d.Saldo);
                var totalHistorico = deudas.Sum(d => d.MontoTotal);
                var totalPagado = deudas.Sum(d => d.MontoPagado);

                return Ok(new
                {
                    success = true,
                    cliente = new
                    {
                        cliente.Id,
                        nombre = $"{cliente.Nombre} {cliente.Apellido}".Trim(),
                        cedula = cliente.Cedula
                    },
                    resumen = new
                    {
                        totalDeudas,
                        deudas_pagadas,
                        deudas_activas,
                        saldoPendiente,
                        totalHistorico,
                        totalPagado
                    },
                    deudas = deudaItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial del cliente {ClienteId}", clienteId);
                return StatusCode(500, new { success = false, message = "Error al obtener historial del cliente", error = ex.Message });
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
                // Estado permanece "pendiente" si aún hay saldo — sin lógica de "vencida"

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
        // Cambia el estado de la deuda manualmente.
        // Estados válidos: pendiente, pagada, cancelada (ya no existe "vencida").
        // ─────────────────────────────────────────────
        [HttpPatch("{deudaId}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid deudaId, [FromBody] CambiarEstadoRequest request)
        {
            try
            {
                // "vencida" eliminado de los estados válidos
                var estadosValidos = new[] { "pendiente", "pagada", "cancelada" };

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