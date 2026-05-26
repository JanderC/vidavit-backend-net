using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    /// <summary>
    /// API: Gestión completa de deudas de clientes.
    /// Rutas base: /api/Deudas
    /// NOTA: Las deudas NO se vencen. Solo existen los estados: pendiente, pagada, cancelada.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DeudasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DeudasController> _logger;

        public DeudasController(AppDbContext context, ILogger<DeudasController> logger)
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
        // GET /api/Deudas
        // Lista todas las deudas con filtros opcionales.
        // Estados posibles: pendiente, pagada, cancelada.
        // ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetDeudas(
            [FromQuery] string? estado = null,
            [FromQuery] string? busqueda = null,
            [FromQuery] bool incluirPagadas = false,
            [FromQuery] int pagina = 1,
            [FromQuery] int porPagina = 50)
        {
            try
            {
                var query = _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .AsQueryable();

                // Buscar por nombre o cédula del cliente
                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    var b = busqueda.ToLower();
                    query = query.Where(d =>
                        d.Cliente.Nombre.ToLower().Contains(b) ||
                        d.Cliente.Apellido.ToLower().Contains(b) ||
                        d.Cliente.Cedula.ToLower().Contains(b) ||
                        d.Concepto.ToLower().Contains(b));
                }

                if (!incluirPagadas)
                    query = query.Where(d => d.Estado != "pagada" && d.Estado != "cancelada");

                var deudas = await query
                    .OrderByDescending(d => d.FechaCreacion)
                    .ToListAsync();

                var resultado = deudas.Select(d => new
                {
                    d.Id,
                    // Estado directo desde BD. Ya no existe el estado "vencida".
                    Estado = d.Estado,
                    d.Concepto,
                    d.MontoTotal,
                    d.MontoPagado,
                    d.Saldo,
                    fechaCreacion = d.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                    fechaVencimiento = d.FechaVencimiento.HasValue
                        ? d.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : (string?)null,
                    diasParaVencer = d.FechaVencimiento.HasValue
                        ? (int)(d.FechaVencimiento.Value.Date - DateTime.Now.Date).TotalDays
                        : (int?)null,
                    venceProximamente = d.FechaVencimiento.HasValue &&
                                        d.FechaVencimiento.Value.Date >= DateTime.Now.Date &&
                                        (d.FechaVencimiento.Value.Date - DateTime.Now.Date).TotalDays <= 7 &&
                                        d.Saldo > 0,
                    cliente = new
                    {
                        d.Cliente.Id,
                        nombre = $"{d.Cliente.Nombre} {d.Cliente.Apellido}".Trim(),
                        cedula = d.Cliente.Cedula
                    }
                }).ToList();

                // Aplicar filtro de estado después de proyectar
                if (!string.IsNullOrWhiteSpace(estado) && estado != "vencida")
                    resultado = resultado.Where(d => d.Estado == estado).ToList();

                // Resumen para el dashboard
                var totalDeudas = resultado.Count;
                var totalPendiente = resultado.Where(d => d.Estado == "pendiente").Sum(d => d.Saldo);
                var countPendientes = resultado.Count(d => d.Estado == "pendiente");
                var countVenceProxim = resultado.Count(d => d.venceProximamente);

                // Paginación
                var total = resultado.Count;
                var items = resultado
                    .Skip((pagina - 1) * porPagina)
                    .Take(porPagina)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    items,
                    total,
                    pagina,
                    porPagina,
                    totalPaginas = (int)Math.Ceiling((double)total / porPagina),
                    resumen = new
                    {
                        totalDeudas,
                        totalPendiente,
                        // Se mantienen estas claves por compatibilidad con el frontend,
                        // pero ya no existen deudas "vencidas".
                        totalVencido = 0m,
                        countVencidas = 0,
                        countPendientes,
                        countVenceProximamente = countVenceProxim
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener deudas");
                return StatusCode(500, new { success = false, message = "Error al obtener deudas", error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // POST /api/Deudas
        // Registrar nueva deuda
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearDeuda([FromBody] CrearDeudaRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                if (request.ClienteId == Guid.Empty)
                    return BadRequest(new { success = false, message = "Debe especificar un cliente válido" });

                var cliente = await _context.Clientes.FindAsync(request.ClienteId);
                if (cliente == null)
                    return NotFound(new { success = false, message = "Cliente no encontrado" });

                var deuda = new DeudaCliente
                {
                    Id = Guid.NewGuid(),
                    ClienteId = request.ClienteId,
                    Concepto = request.Concepto ?? "Deuda sin concepto",
                    MontoTotal = request.Monto,
                    MontoPagado = 0,
                    Saldo = request.Monto,
                    Estado = "pendiente",   // Siempre inicia en pendiente
                    FechaCreacion = DateTime.Now,
                    FechaVencimiento = request.FechaVencimiento
                };

                _context.DeudasClientes.Add(deuda);
                await _context.SaveChangesAsync();

                _logger.LogInformation("📝 Nueva deuda creada: {DeudaId} — Cliente {ClienteId} — ${Monto}",
                    deuda.Id, request.ClienteId, request.Monto);

                return Ok(new
                {
                    success = true,
                    message = $"Deuda de ${request.Monto:N2} registrada para {cliente.Nombre} {cliente.Apellido}",
                    deuda = new
                    {
                        deuda.Id,
                        deuda.Estado,
                        deuda.Concepto,
                        deuda.MontoTotal,
                        deuda.MontoPagado,
                        deuda.Saldo,
                        fechaCreacion = deuda.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaVencimiento = deuda.FechaVencimiento.HasValue
                            ? deuda.FechaVencimiento.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                            : (string?)null,
                        cliente = new
                        {
                            cliente.Id,
                            nombre = $"{cliente.Nombre} {cliente.Apellido}".Trim(),
                            cedula = cliente.Cedula
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear deuda");
                return StatusCode(500, new { success = false, message = "Error al crear deuda", error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // POST /api/Deudas/abonar
        // Registra un abono a una deuda
        // ─────────────────────────────────────────────
        [HttpPost("abonar")]
        public async Task<IActionResult> Abonar([FromBody] AbonarRequest request)
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

                // Registrar abono en AbonosDeuda
                var abono = new AbonoDeuda
                {
                    Id = Guid.NewGuid(),
                    DeudaId = deuda.Id,
                    Monto = request.Monto,
                    MetodoPago = metodoPago,
                    FechaAbono = DateTime.Now,
                    Notas = string.IsNullOrWhiteSpace(request.Notas)
                        ? $"Abono — {deuda.Concepto}"
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

                // Actualizar deuda
                deuda.MontoPagado += request.Monto;
                deuda.Saldo = deuda.MontoTotal - deuda.MontoPagado;

                if (deuda.Saldo <= 0)
                {
                    deuda.Saldo = 0;
                    deuda.Estado = "pagada";
                    _logger.LogInformation("✅ Deuda {DeudaId} marcada como PAGADA", deuda.Id);
                }
                // No hay más lógica de "vencida" — el estado permanece "pendiente"

                await _context.SaveChangesAsync();

                _logger.LogInformation("💰 Abono registrado: ${Monto} → Deuda {DeudaId}", request.Monto, deuda.Id);

                return Ok(new
                {
                    success = true,
                    message = deuda.Estado == "pagada"
                        ? $"Deuda pagada completamente por ${request.Monto:N2}"
                        : $"Abono de ${request.Monto:N2} registrado. Saldo: ${deuda.Saldo:N2}",
                    estadoDeuda = deuda.Estado,
                    saldoRestante = deuda.Saldo,
                    montoPagado = deuda.MontoPagado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar abono");
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
        // MODELOS DE REQUEST
        // ─────────────────────────────────────────────

        public class CrearDeudaRequest
        {
            public Guid ClienteId { get; set; }
            public string? Concepto { get; set; }
            public decimal Monto { get; set; }
            public DateTime? FechaVencimiento { get; set; }
        }

        public class AbonarRequest
        {
            public Guid DeudaId { get; set; }
            public decimal Monto { get; set; }
            public string? MetodoPago { get; set; }
            public string? Notas { get; set; }
        }
    }
}