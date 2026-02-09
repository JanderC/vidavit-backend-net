using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFit.Services;
using VidaFitBackend.Services;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckInController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFingerprintService _fingerprintService;
        private readonly INotificationService _notificationService;

        public CheckInController(
            AppDbContext context,
            IFingerprintService fingerprintService,
            INotificationService notificationService)
        {
            _context = context;
            _fingerprintService = fingerprintService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Realizar check-in usando número de cédula
        /// </summary>
        [HttpPost("cedula")]
        public async Task<IActionResult> CheckInByCedula([FromBody] CedulaRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Cedula))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Cédula es requerida"
                    });
                }

                var cliente = await _context.Clientes
                    .Include(c => c.Membresias)
                    .FirstOrDefaultAsync(c => c.Cedula == request.Cedula && c.Activo);

                if (cliente == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Cédula no encontrada. Acércate a recepción."
                    });
                }

                // ========== VALIDACIÓN DE CHECK-IN DUPLICADO (12 HORAS) ==========
                var hace12Horas = DateTime.Now.AddHours(-12);
                var checkInReciente = await _context.CheckIns
                    .Where(c => c.ClienteId == cliente.Id &&
                                c.Exitoso &&
                                c.FechaHora >= hace12Horas)
                    .OrderByDescending(c => c.FechaHora)
                    .FirstOrDefaultAsync();

                if (checkInReciente != null)
                {
                    var horasTranscurridas = (DateTime.Now - checkInReciente.FechaHora).TotalHours;
                    var horasRestantes = 12 - horasTranscurridas;

                    return Ok(new
                    {
                        success = false,
                        message = $"Ya realizaste check-in hoy a las {checkInReciente.FechaHora:hh:mm tt}",
                        yaHizoCheckIn = true,
                        ultimoCheckIn = new
                        {
                            fecha = checkInReciente.FechaHora,
                            horaFormateada = checkInReciente.FechaHora.ToString("hh:mm tt"),
                            horasTranscurridas = Math.Round(horasTranscurridas, 1),
                            horasRestantes = Math.Round(horasRestantes, 1)
                        },
                        cliente = new
                        {
                            nombre = $"{cliente.Nombre} {cliente.Apellido}",
                            fotoBase64 = cliente.FotoBase64
                        }
                    });
                }
                // ==================================================================

                // Verificar deudas pendientes
                var deudasPendientes = await _context.DeudasClientes
                    .Where(d => d.ClienteId == cliente.Id && d.Estado != "pagada")
                    .ToListAsync();

                var totalDeuda = deudasPendientes.Sum(d => d.Saldo);

                // Verificar membresía
                var membresiaActiva = cliente.Membresias
                    .Where(m => m.Estado == "activa")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .FirstOrDefault();

                bool tieneAcceso = true;
                string mensaje;
                int? diasRestantes = null;

                if (membresiaActiva == null)
                {
                    tieneAcceso = false;
                    mensaje = "No tienes membresía activa. Acércate a recepción.";
                }
                else if (membresiaActiva.FechaVencimiento < DateTime.Now.Date)
                {
                    tieneAcceso = false;
                    mensaje = "Tu membresía ha vencido. Acércate a recepción para renovar.";
                    membresiaActiva.Estado = "vencida";
                    await _context.SaveChangesAsync();
                }
                else
                {
                    diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.Now.Date).Days;
                    mensaje = diasRestantes <= 3
                        ? $"¡Bienvenido! Tu membresía vence en {diasRestantes} días"
                        : "¡Bienvenido!";
                }

                // Registrar check-in
                var checkIn = new CheckIn
                {
                    ClienteId = cliente.Id,
                    FechaHora = DateTime.Now,
                    Metodo = "cedula",
                    Exitoso = tieneAcceso,
                    Nota = mensaje
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                // RETORNO con información de deuda
                return Ok(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    cliente = new
                    {
                        nombre = $"{cliente.Nombre} {cliente.Apellido}",
                        fotoBase64 = cliente.FotoBase64,
                        diasRestantes
                    },
                    membresia = membresiaActiva != null ? new
                    {
                        estado = membresiaActiva.Estado,
                        mensaje = mensaje,
                        diasRestantes = diasRestantes,
                        fechaVencimiento = membresiaActiva.FechaVencimiento,
                        diasVencidos = membresiaActiva.FechaVencimiento < DateTime.Now
                            ? (DateTime.Now.Date - membresiaActiva.FechaVencimiento).Days
                            : (int?)null
                    } : null,
                    deuda = totalDeuda > 0 ? new
                    {
                        tieneDeuda = true,
                        montoTotal = totalDeuda,
                        cantidadDeudas = deudasPendientes.Count
                    } : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al procesar check-in",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener check-ins del día actual
        /// </summary>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayCheckIns()
        {
            try
            {
                // Obtener todos los check-ins sin cerrar (no filtrar por fecha en la query)
                var todosCheckIns = await _context.CheckIns
                    .Include(c => c.Cliente)
                    .OrderByDescending(c => c.FechaHora)
                    .ToListAsync();

                // Filtrar en memoria comparando la fecha UTC con la fecha local de hoy
                var today = DateTime.Now.Date;
                var checkIns = todosCheckIns
                    .Where(c => c.FechaHora.ToLocalTime().Date == today)
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaHora,
                        clienteNombre = $"{c.Cliente.Nombre} {c.Cliente.Apellido}",
                        c.Metodo,
                        c.Exitoso,
                        c.Nota,
                        fotoCliente = c.Cliente.FotoBase64
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    total = checkIns.Count,
                    checkIns
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener check-ins",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener check-ins con filtro de fechas
        /// </summary>
        [HttpGet("rango")]
        public async Task<IActionResult> GetCheckInsPorRango([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            try
            {
                // Obtener fechas locales
                var fechaDesde = desde?.Date ?? DateTime.Now.Date;
                var fechaHasta = hasta?.Date ?? DateTime.Now.Date;

                // Obtener todos los check-ins
                var todosCheckIns = await _context.CheckIns
                    .Include(c => c.Cliente)
                    .OrderByDescending(c => c.FechaHora)
                    .ToListAsync();

                // Filtrar en memoria comparando fechas locales
                var checkIns = todosCheckIns
                    .Where(c => {
                        var fechaLocal = c.FechaHora.ToLocalTime().Date;
                        return fechaLocal >= fechaDesde && fechaLocal <= fechaHasta;
                    })
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaHora,
                        clienteNombre = $"{c.Cliente.Nombre} {c.Cliente.Apellido}",
                        c.Metodo,
                        c.Exitoso,
                        c.Nota,
                        fotoCliente = c.Cliente.FotoBase64
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    total = checkIns.Count,
                    desde = fechaDesde,
                    hasta = fechaHasta,
                    checkIns
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener check-ins",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener historial de check-ins de un cliente
        /// </summary>
        [HttpGet("cliente/{clienteId}")]
        public async Task<IActionResult> GetClienteCheckIns(Guid clienteId, [FromQuery] int limit = 50)
        {
            try
            {
                var checkIns = await _context.CheckIns
                    .Where(c => c.ClienteId == clienteId)
                    .OrderByDescending(c => c.FechaHora)
                    .Take(limit)
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaHora,
                        c.Metodo,
                        c.Exitoso,
                        c.Nota
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    total = checkIns.Count,
                    checkIns
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener historial",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// NUEVO: Obtener nombres de clientes que asistieron en una fecha específica
        /// </summary>
        [HttpGet("asistentes/{fecha}")]
        public async Task<IActionResult> GetAsistentesPorFecha(DateTime fecha)
        {
            try
            {
                var fechaBuscada = fecha.Date;

                // Obtener todos los check-ins exitosos
                var todosCheckIns = await _context.CheckIns
                    .Include(c => c.Cliente)
                    .Where(c => c.Exitoso)
                    .OrderBy(c => c.FechaHora)
                    .ToListAsync();

                // Filtrar por fecha en memoria
                var asistentes = todosCheckIns
                    .Where(c => c.FechaHora.ToLocalTime().Date == fechaBuscada)
                    .Select(c => new
                    {
                        c.Id,
                        clienteId = c.ClienteId,
                        nombre = $"{c.Cliente.Nombre} {c.Cliente.Apellido}",
                        cedula = c.Cliente.Cedula,
                        hora = c.FechaHora.ToString("hh:mm tt"),
                        metodo = c.Metodo,
                        fotoBase64 = c.Cliente.FotoBase64
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    fecha = fechaBuscada,
                    total = asistentes.Count,
                    asistentes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener asistentes",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verificar estado del lector de huellas
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetReaderStatus()
        {
            var connected = _fingerprintService.IsReaderConnected();

            return Ok(new
            {
                success = true,
                connected,
                message = connected
                    ? "Lector conectado y listo"
                    : "Lector no detectado. Verifica la conexión USB."
            });
        }

        /// <summary>
        /// Realizar check-in manual por un administrador
        /// </summary>
        [HttpPost("manual")]
        public async Task<IActionResult> ManualCheckIn([FromBody] ManualCheckInRequest request)
        {
            try
            {
                var cliente = await _context.Clientes
                    .Include(c => c.Membresias)
                    .FirstOrDefaultAsync(c => c.Id == request.ClienteId);

                if (cliente == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Cliente no encontrado"
                    });
                }

                // Registrar check-in
                var checkIn = new CheckIn
                {
                    ClienteId = cliente.Id,
                    FechaHora = request.FechaHora ?? DateTime.Now,
                    Metodo = "manual",
                    Exitoso = true,
                    Nota = request.Nota ?? "Check-in manual por administrador"
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Check-in manual registrado exitosamente",
                    checkIn = new
                    {
                        checkIn.Id,
                        checkIn.FechaHora,
                        clienteNombre = $"{cliente.Nombre} {cliente.Apellido}"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al registrar check-in manual",
                    error = ex.Message
                });
            }
        }
    }

    // ==================== DTOs ====================

    public class CedulaRequest
    {
        public string Cedula { get; set; }
    }

    public class ManualCheckInRequest
    {
        public Guid ClienteId { get; set; }
        public DateTime? FechaHora { get; set; }
        public string Nota { get; set; }
    }
}