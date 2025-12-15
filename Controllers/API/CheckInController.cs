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
                else if (membresiaActiva.FechaVencimiento < DateTime.UtcNow.Date)
                {
                    tieneAcceso = false;
                    mensaje = "Tu membresía ha vencido. Acércate a recepción para renovar.";
                    membresiaActiva.Estado = "vencida";
                    await _context.SaveChangesAsync();
                }
                else
                {
                    diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.UtcNow.Date).Days;
                    mensaje = diasRestantes <= 3
                        ? $"¡Bienvenido! Tu membresía vence en {diasRestantes} días"
                        : "¡Bienvenido!";
                }

                // ⚠️ CAMBIO CRÍTICO: Usar DateTime.UtcNow en lugar de DateTime.Now
                var checkIn = new CheckIn
                {
                    ClienteId = cliente.Id,
                    FechaHora = DateTime.UtcNow,  // ✅ CAMBIO AQUÍ
                    Metodo = "cedula",
                    Exitoso = tieneAcceso,
                    Nota = mensaje
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                // RETORNO SIMPLIFICADO igual que fingerprint
                return Ok(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    cliente = new
                    {
                        nombre = $"{cliente.Nombre} {cliente.Apellido}",
                        fotoBase64 = cliente.FotoBase64,
                        diasRestantes
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
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayCheckIns()
        {
            try
            {
                var today = DateTime.UtcNow.Date;  // ✅ CAMBIO AQUÍ
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

                // ⚠️ CAMBIO CRÍTICO: Usar DateTime.UtcNow
                var checkIn = new CheckIn
                {
                    ClienteId = cliente.Id,
                    FechaHora = request.FechaHora ?? DateTime.UtcNow,  // ✅ CAMBIO AQUÍ
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