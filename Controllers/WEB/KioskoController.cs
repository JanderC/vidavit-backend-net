using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using VidaFitBackend.Services;

namespace VidaFit.Controllers.WEB
{
    [Route("kiosko")]
    public class KioskoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFingerprintService _fingerprintService;

        public KioskoController(AppDbContext context, IFingerprintService fingerprintService)
        {
            _context = context;
            _fingerprintService = fingerprintService;
        }

        // GET: /kiosko
        [HttpGet("")]
        public IActionResult Index()
        {
            ViewBag.ReaderConnected = _fingerprintService.IsReaderConnected();
            return View();
        }

        // POST: /kiosko/checkin-huella (NUEVO ENDPOINT)
        [HttpPost("checkin-huella")]
        public async Task<IActionResult> CheckInPorHuella()
        {
            try
            {
                // Verificar que el lector esté conectado
                if (!_fingerprintService.IsReaderConnected())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Lector de huellas no conectado",
                        alertType = "error"
                    });
                }

                // Obtener todos los clientes activos con huellas
                var clientes = await _context.Clientes
                    .Include(c => c.Membresias)
                    .Where(c => c.Activo && c.HuellaTemplate != null)
                    .ToListAsync();

                if (clientes.Count == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No hay clientes con huellas registradas",
                        alertType = "error"
                    });
                }

                // Preparar arrays para verificación
                string[] allTemplates = clientes.Select(c => c.HuellaTemplate!).ToArray();
                string[] clientNames = clientes.Select(c => $"{c.Nombre} {c.Apellido}").ToArray();

                // Verificar huella con múltiples capturas
                var (matched, clientName, similarity) = _fingerprintService.VerifyWithMultipleCaptures(allTemplates, clientNames);

                if (!matched)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Huella no reconocida. Por favor ingresa tu cédula.",
                        alertType = "warning",
                        useCedula = true, // Bandera para cambiar a modo cédula
                        similarity = similarity
                    });
                }

                // Buscar cliente encontrado
                var clienteEncontrado = clientes.FirstOrDefault(c => $"{c.Nombre} {c.Apellido}" == clientName);

                if (clienteEncontrado == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Error al identificar cliente",
                        alertType = "error"
                    });
                }

                // ========== VALIDACIÓN DE CHECK-IN DUPLICADO (12 HORAS) ==========
                var hace12Horas = DateTime.Now.AddHours(-12);
                var checkInReciente = await _context.CheckIns
                    .Where(c => c.ClienteId == clienteEncontrado.Id &&
                                c.Exitoso &&
                                c.FechaHora >= hace12Horas)
                    .OrderByDescending(c => c.FechaHora)
                    .FirstOrDefaultAsync();

                if (checkInReciente != null)
                {
                    var horasTranscurridas = (DateTime.Now - checkInReciente.FechaHora).TotalHours;
                    var horasRestantes = 12 - horasTranscurridas;

                    return Json(new
                    {
                        success = false,
                        message = $"Ya realizaste check-in hoy a las {checkInReciente.FechaHora:hh:mm tt}",
                        alertType = "warning",
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
                            nombre = $"{clienteEncontrado.Nombre} {clienteEncontrado.Apellido}",
                            fotoBase64 = clienteEncontrado.FotoBase64
                        }
                    });
                }
                // ==================================================================

                // Verificar membresía
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
                    Nota = $"Verificación biométrica: {similarity:F1}%"
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = tieneAcceso,
                    message = mensaje,
                    alertType = alertType,
                    cliente = new
                    {
                        nombre = $"{clienteEncontrado.Nombre} {clienteEncontrado.Apellido}",
                        fotoBase64 = clienteEncontrado.FotoBase64
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
                    diasRestantes = diasRestantes,
                    fechaVencimiento = membresiaActiva?.FechaVencimiento,
                    similarity = similarity
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR en CheckInPorHuella: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Error al procesar huella. Intenta con tu cédula.",
                    alertType = "error",
                    useCedula = true,
                    error = ex.Message
                });
            }
        }

        // POST: /kiosko/checkin-cedula
        [HttpPost("checkin-cedula")]
        public async Task<IActionResult> CheckInPorCedula([FromBody] CedulaCheckInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Cedula))
            {
                return Json(new
                {
                    success = false,
                    message = "Debes ingresar una cédula válida.",
                    alertType = "error"
                });
            }

            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                .FirstOrDefaultAsync(c => c.Cedula == request.Cedula && c.Activo);

            if (cliente == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Cédula no encontrada. Acércate a recepción.",
                    alertType = "error"
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

                return Json(new
                {
                    success = false,
                    message = $"Ya realizaste check-in hoy a las {checkInReciente.FechaHora:hh:mm tt}",
                    alertType = "warning",
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

            return Json(new
            {
                success = tieneAcceso,
                message = mensaje,
                alertType = alertType,
                cliente = new
                {
                    nombre = $"{cliente.Nombre} {cliente.Apellido}",
                    fotoBase64 = cliente.FotoBase64
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
                diasRestantes = diasRestantes,
                fechaVencimiento = membresiaActiva?.FechaVencimiento
            });
        }

        // GET: /kiosko/usar-cedula
        [HttpGet("usar-cedula")]
        public IActionResult UsarCedula()
        {
            return View();
        }
    }

    // DTO para recibir cédula en formato JSON
    public class CedulaCheckInRequest
    {
        public string Cedula { get; set; }
    }
}