using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFit.Services;
using VidaFitBackend.Models;
using VidaFitBackend.Services;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class FingerprintController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFingerprintService _fingerprintService;

        public FingerprintController(AppDbContext context, IFingerprintService fingerprintService)
        {
            _context = context;
            _fingerprintService = fingerprintService;
        }

        /// <summary>
        /// Check-in con múltiples capturas (3 intentos) para mejor precisión
        /// </summary>
        [HttpPost("checkin-multi")]
        public async Task<IActionResult> CheckInMultiCapture()
        {
            try
            {
                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   🔵 CHECK-IN CON MÚLTIPLES CAPTURAS");
                Console.WriteLine("═══════════════════════════════════════════");

                // Verificar que el lector esté conectado
                if (!_fingerprintService.IsReaderConnected())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Lector de huellas no conectado"
                    });
                }

                // Obtener todos los clientes con huellas registradas
                var clientes = await _context.Clientes
                    .Include(c => c.Membresias)
                    .Where(c => c.Activo && c.HuellaTemplate != null)
                    .ToListAsync();

                if (clientes.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No hay clientes con huellas registradas"
                    });
                }

                // Preparar arrays para el servicio de verificación
                string[] allTemplates = clientes.Select(c => c.HuellaTemplate!).ToArray();
                string[] clientNames = clientes.Select(c => $"{c.Nombre} {c.Apellido}").ToArray();

                // Realizar verificación con múltiples capturas (3 intentos)
                var (matched, clientName, similarity) = _fingerprintService.VerifyWithMultipleCaptures(allTemplates, clientNames);

                if (!matched)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Huella no reconocida. Acércate a recepción.",
                        similarity = similarity
                    });
                }

                // Buscar el cliente encontrado
                var clienteEncontrado = clientes.FirstOrDefault(c => $"{c.Nombre} {c.Apellido}" == clientName);

                if (clienteEncontrado == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Error al identificar cliente"
                    });
                }

                // Verificar membresía activa
                var membresiaActiva = clienteEncontrado.Membresias
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
                    mensaje = "Tu membresía ha vencido. Renueva en recepción.";
                    membresiaActiva.Estado = "vencida";
                    await _context.SaveChangesAsync();
                }
                else
                {
                    diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.Now.Date).Days;
                    mensaje = diasRestantes <= 3
                        ? $"¡Bienvenido! Tu membresía vence en {diasRestantes} días"
                        : $"¡Bienvenido {clienteEncontrado.Nombre}!";
                }

                // Registrar check-in
                //var checkIn = new CheckIn
                //{
                //    Id = Guid.NewGuid(),
                //    ClienteId = clienteEncontrado.Id,
                //    FechaHora = DateTime.Now,
                //    Metodo = "huella-multi",
                //    Exitoso = tieneAcceso,
                //    Nota = $"Multi-captura ({similarity:F1}%)",
                //    CreatedAt = DateTime.Now,
                //    UpdatedAt = DateTime.Now
                //};

                //_context.CheckIns.Add(checkIn);
                //await _context.SaveChangesAsync();

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ✅ CHECK-IN EXITOSO: {clientName}");
                Console.WriteLine($"   Similitud: {similarity:F1}%");
                Console.WriteLine($"   Días restantes: {diasRestantes ?? 0}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                // RETORNO SIMPLIFICADO: solo nombre completo y días restantes
                return Ok(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    similarity = similarity,
                    cliente = new
                    {
                        nombre = clientName, // Nombre completo ya concatenado
                        fotoBase64 = clienteEncontrado.FotoBase64
                    },
                    membresia = membresiaActiva != null ? new
                    {
                        diasRestantes
                    } : null
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                return Ok(new
                {
                    success = false,
                    message = "Error al procesar huella. Intenta nuevamente.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Capturar y registrar huella para un cliente
        /// </summary>
        [HttpPost("capture/{clienteId}")]
        public async Task<IActionResult> CaptureFingerprint(Guid clienteId)
        {
            try
            {
                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Cliente no encontrado"
                    });
                }

                if (!_fingerprintService.IsReaderConnected())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Lector de huellas no conectado"
                    });
                }

                // Capturar huella
                byte[] fingerprintData = _fingerprintService.CaptureFingerprint();
                string template = Convert.ToBase64String(fingerprintData);

                // Guardar en cliente
                cliente.HuellaTemplate = template;
                // NO asignar UpdatedAt si el modelo Cliente no lo tiene

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Huella capturada y registrada exitosamente"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al capturar huella: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Verificar estado del lector
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var connected = _fingerprintService.IsReaderConnected();
            return Ok(new
            {
                success = true,
                connected,
                message = connected ? "Lector conectado" : "Lector no conectado"
            });
        }
    }
}