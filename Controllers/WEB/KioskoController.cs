using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.WEB
{
    [Route("kiosko")]
    public class KioskoController : Controller
    {
        private readonly AppDbContext _context;

        public KioskoController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /kiosko
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
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
                    .ThenInclude(m => m.Plan)
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

            // Verificar deudas pendientes
            var deudasPendientes = await _context.DeudasClientes
                .Where(d => d.ClienteId == cliente.Id && d.Estado != "pagada")
                .ToListAsync();

            var totalDeuda = deudasPendientes.Sum(d => d.Saldo);

            // Buscar membresía activa vigente
            var membresiaActiva = cliente.Membresias
                .Where(m => m.Estado == "activa" && m.FechaVencimiento >= DateTime.Now.Date)
                .OrderByDescending(m => m.FechaVencimiento)
                .FirstOrDefault();

            // Si no hay activa vigente, buscar la más reciente (puede estar vencida)
            var membresiaReciente = membresiaActiva ?? cliente.Membresias
                .OrderByDescending(m => m.FechaVencimiento)
                .FirstOrDefault();

            bool tieneAcceso = true;
            string mensaje = $"¡Bienvenido {cliente.Nombre} {cliente.Apellido}!";
            string alertType = "success";
            int? diasRestantes = null;
            int? diasVencidos = null;

            if (membresiaReciente == null)
            {
                tieneAcceso = false;
                mensaje = "No tienes membresía registrada. Acércate a recepción.";
                alertType = "error";
            }
            else if (membresiaActiva == null)
            {
                tieneAcceso = false;
                diasVencidos = (DateTime.Now.Date - membresiaReciente.FechaVencimiento.Date).Days;
                mensaje = "Tu membresía ha vencido. Acércate a recepción para renovar.";
                alertType = "error";

                if (membresiaReciente.Estado != "vencida")
                {
                    membresiaReciente.Estado = "vencida";
                    membresiaReciente.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                diasRestantes = (membresiaActiva.FechaVencimiento.Date - DateTime.Now.Date).Days;

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

            var membresiaRespuesta = membresiaActiva ?? membresiaReciente;

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
                membresia = membresiaRespuesta != null ? new
                {
                    estado = membresiaRespuesta.Estado,
                    tipoPlan = membresiaRespuesta.Plan?.Nombre ?? "Sin plan",
                    mensaje = mensaje,
                    diasRestantes = diasRestantes,
                    diasVencidos = diasVencidos,
                    fechaInicio = membresiaRespuesta.FechaInicio,
                    fechaVencimiento = membresiaRespuesta.FechaVencimiento
                } : null,
                diasRestantes = diasRestantes,
                fechaVencimiento = membresiaRespuesta?.FechaVencimiento,
                deuda = totalDeuda > 0 ? new
                {
                    tieneDeuda = true,
                    montoTotal = totalDeuda,
                    cantidadDeudas = deudasPendientes.Count
                } : null
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