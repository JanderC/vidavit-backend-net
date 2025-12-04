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
        /// Realizar check-in usando huella digital
        /// </summary>
        /// <returns>Resultado del check-in con información del cliente</returns>
        [HttpPost("fingerprint")]
        public async Task<IActionResult> CheckInByFingerprint()
        {
            try
            {
                // Verificar que el lector esté conectado
                if (!_fingerprintService.IsReaderConnected())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Lector de huellas no conectado",
                        code = "READER_NOT_CONNECTED"
                    });
                }

                // Capturar huella del lector
                byte[] fingerprintData;
                try
                {
                    fingerprintData = _fingerprintService.CaptureFingerprint();
                }
                catch (Exception ex)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Error al capturar huella. Intenta nuevamente.",
                        code = "CAPTURE_ERROR",
                        error = ex.Message
                    });
                }

                // Buscar cliente con huella coincidente
                var clientes = await _context.Clientes
                    .Include(c => c.Membresias)
                    .Where(c => c.Activo && c.HuellaTemplate != null)
                    .ToListAsync();

                Cliente clienteEncontrado = null;

                foreach (var cliente in clientes)
                {
                    if (_fingerprintService.VerifyFingerprint(fingerprintData, cliente.HuellaTemplate))
                    {
                        clienteEncontrado = cliente;
                        break;
                    }
                }

                if (clienteEncontrado == null)
                {
                    // Registrar intento fallido
                    var checkInFallido = new CheckIn
                    {
                        ClienteId = Guid.Empty,
                        FechaHora = DateTime.Now,
                        Metodo = "huella",
                        Exitoso = false,
                        Nota = "Huella no reconocida"
                    };

                    return Ok(new
                    {
                        success = false,
                        message = "Huella no reconocida. Acércate a recepción.",
                        code = "FINGERPRINT_NOT_FOUND"
                    });
                }

                // Verificar membresía activa
                var membresiaActiva = clienteEncontrado.Membresias
                    .Where(m => m.Estado == "activa")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .FirstOrDefault();

                bool tieneAcceso = true;
                string mensaje = $"¡Bienvenido {clienteEncontrado.Nombre} {clienteEncontrado.Apellido}!";
                string alertType = "success";
                int? diasRestantes = null;

                if (membresiaActiva == null)
                {
                    tieneAcceso = false;
                    mensaje = "No tienes membresía activa. Acércate a recepción.";
                    alertType = "error";
                }
                else if (membresiaActiva.FechaVencimiento < DateTime.Now.Date)
                {
                    tieneAcceso = false;
                    mensaje = "Tu membresía ha vencido. Acércate a recepción para renovar.";
                    alertType = "error";

                    // Actualizar estado de membresía
                    membresiaActiva.Estado = "vencida";
                    membresiaActiva.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.Now.Date).Days;

                    if (diasRestantes <= 3)
                    {
                        mensaje = $"¡Bienvenido! Tu membresía vence en {diasRestantes} días.";
                        alertType = "warning";
                    }
                    else if (diasRestantes <= 7)
                    {
                        mensaje = $"¡Bienvenido! Tu membresía vence en {diasRestantes} días.";
                        alertType = "info";
                    }
                }

                // Registrar check-in
                var checkIn = new CheckIn
                {
                    ClienteId = clienteEncontrado.Id,
                    FechaHora = DateTime.Now,
                    Metodo = "huella",
                    Exitoso = tieneAcceso,
                    Nota = mensaje
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    alertType,
                    cliente = new
                    {
                        id = clienteEncontrado.Id,
                        nombre = $"{clienteEncontrado.Nombre} {clienteEncontrado.Apellido}",
                        fotoBase64 = clienteEncontrado.FotoBase64,
                        diasRestantes,
                        fechaVencimiento = membresiaActiva?.FechaVencimiento
                    }
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
        /// Realizar check-in usando número de cédula
        /// </summary>
        /// <param name="request">Objeto con el número de cédula</param>
        /// <returns>Resultado del check-in con información del cliente</returns>
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
                        message = "Cédula no encontrada. Acércate a recepción.",
                        code = "CEDULA_NOT_FOUND"
                    });
                }

                // Verificar membresía (mismo código que fingerprint)
                var membresiaActiva = cliente.Membresias
                    .Where(m => m.Estado == "activa")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .FirstOrDefault();

                bool tieneAcceso = true;
                string mensaje = $"¡Bienvenido {cliente.Nombre} {cliente.Apellido}!";
                string alertType = "success";
                int? diasRestantes = null;

                if (membresiaActiva == null)
                {
                    tieneAcceso = false;
                    mensaje = "No tienes membresía activa. Acércate a recepción.";
                    alertType = "error";
                }
                else if (membresiaActiva.FechaVencimiento < DateTime.Now.Date)
                {
                    tieneAcceso = false;
                    mensaje = "Tu membresía ha vencido. Acércate a recepción para renovar.";
                    alertType = "error";
                    membresiaActiva.Estado = "vencida";
                    membresiaActiva.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.Now.Date).Days;

                    if (diasRestantes <= 3)
                    {
                        mensaje = $"¡Bienvenido! Tu membresía vence en {diasRestantes} días.";
                        alertType = "warning";
                    }
                    else if (diasRestantes <= 7)
                    {
                        mensaje = $"¡Bienvenido! Tu membresía vence en {diasRestantes} días.";
                        alertType = "info";
                    }
                }

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

                return Ok(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    alertType,
                    cliente = new
                    {
                        id = cliente.Id,
                        nombre = $"{cliente.Nombre} {cliente.Apellido}",
                        fotoBase64 = cliente.FotoBase64,
                        diasRestantes,
                        fechaVencimiento = membresiaActiva?.FechaVencimiento
                    }
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
        /// <returns>Lista de check-ins de hoy</returns>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayCheckIns()
        {
            try
            {
                var today = DateTime.Now.Date;
                var checkIns = await _context.CheckIns
                    .Include(c => c.Cliente)
                    .Where(c => c.FechaHora.Date == today)
                    .OrderByDescending(c => c.FechaHora)
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
                    message = "Error al obtener check-ins",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener historial de check-ins de un cliente
        /// </summary>
        /// <param name="clienteId">ID del cliente</param>
        /// <param name="limit">Cantidad de registros (default: 50)</param>
        /// <returns>Historial de check-ins</returns>
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
        /// Obtener estadísticas de check-ins por período
        /// </summary>
        /// <param name="dias">Días hacia atrás (default: 30)</param>
        /// <returns>Estadísticas de check-ins</returns>
        [HttpGet("estadisticas")]
        public async Task<IActionResult> GetEstadisticas([FromQuery] int dias = 30)
        {
            try
            {
                var fechaInicio = DateTime.Now.AddDays(-dias).Date;

                var checkIns = await _context.CheckIns
                    .Where(c => c.FechaHora >= fechaInicio)
                    .ToListAsync();

                var estadisticas = checkIns
                    .GroupBy(c => c.FechaHora.Date)
                    .Select(g => new
                    {
                        fecha = g.Key,
                        total = g.Count(),
                        exitosos = g.Count(c => c.Exitoso),
                        fallidos = g.Count(c => !c.Exitoso),
                        porHuella = g.Count(c => c.Metodo == "huella"),
                        porCedula = g.Count(c => c.Metodo == "cedula")
                    })
                    .OrderBy(e => e.fecha)
                    .ToList();

                var totalCheckIns = checkIns.Count;
                var promediosDiario = totalCheckIns > 0 ? totalCheckIns / dias : 0;
                var clientesUnicos = checkIns.Select(c => c.ClienteId).Distinct().Count();

                return Ok(new
                {
                    success = true,
                    periodo = new
                    {
                        fechaInicio,
                        fechaFin = DateTime.Now.Date,
                        dias
                    },
                    resumen = new
                    {
                        totalCheckIns,
                        promediosDiario,
                        clientesUnicos
                    },
                    estadisticas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener estadísticas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verificar estado del lector de huellas
        /// </summary>
        /// <returns>Estado de conexión del lector</returns>
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
        /// <param name="request">Datos del check-in manual</param>
        /// <returns>Confirmación del check-in</returns>
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