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
            // Verificar si el lector de huellas está conectado
            ViewBag.ReaderConnected = _fingerprintService.IsReaderConnected();

            // Devuelve la vista principal del kiosko (pantalla de check-in)
            return View();
        }

        // POST: /kiosko/checkin-cedula
        [HttpPost("checkin-cedula")]
        public async Task<IActionResult> CheckInPorCedula([FromForm] string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
            {
                ViewBag.Error = "Debes ingresar una cédula válida.";
                return View("Index");
            }

            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                .FirstOrDefaultAsync(c => c.Cedula == cedula && c.Activo);

            if (cliente == null)
            {
                ViewBag.Error = "Cédula no encontrada. Acércate a recepción.";
                return View("Index");
            }

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

            ViewBag.Mensaje = mensaje;
            ViewBag.AlertType = alertType;
            ViewBag.Cliente = cliente;
            ViewBag.DiasRestantes = diasRestantes;
            ViewBag.FechaVencimiento = membresiaActiva?.FechaVencimiento;

            return View("Resultado");
        }

        // GET: /kiosko/usar-cedula
        [HttpGet("usar-cedula")]
        public IActionResult UsarCedula()
        {
            // Muestra la pantalla para ingresar cédula manualmente
            return View();
        }
    }
}