using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Linq;

namespace VidaFit.Controllers.WEB
{
    [Route("clientes")]
    public class ClientesController : Controller
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /clientes
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var clientes = await _context.Clientes
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(clientes);
        }

        // GET: /clientes/detalle/{id}
        [HttpGet("detalle/{id}")]
        public async Task<IActionResult> Detalle(Guid id)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                .Include(c => c.CheckIns)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
                return NotFound();

            return View(cliente);
        }

        // GET: /clientes/crear
        [HttpGet("crear")]
        public IActionResult Crear()
        {
            return View();
        }

        // POST: /clientes/crear
        [HttpPost("crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Cliente cliente)
        {
            // Remover errores de validación de campos opcionales que pueden venir vacíos
            ModelState.Remove("Telefono");
            ModelState.Remove("Email");
            ModelState.Remove("FechaNacimiento");
            ModelState.Remove("Direccion");
            ModelState.Remove("FotoBase64");
            ModelState.Remove("HuellaTemplate");
            ModelState.Remove("HuellaDigital");
            ModelState.Remove("Membresias");
            ModelState.Remove("CheckIns");

            if (ModelState.IsValid)
            {
                cliente.Id = Guid.NewGuid();
                cliente.CreatedAt = DateTime.UtcNow;
                cliente.UpdatedAt = DateTime.UtcNow;
                cliente.Activo = true;

                // Asegurar que los campos opcionales sean null si están vacíos
                if (string.IsNullOrWhiteSpace(cliente.Telefono))
                    cliente.Telefono = null;
                if (string.IsNullOrWhiteSpace(cliente.Email))
                    cliente.Email = null;
                if (string.IsNullOrWhiteSpace(cliente.Direccion))
                    cliente.Direccion = null;
                if (string.IsNullOrWhiteSpace(cliente.FotoBase64))
                    cliente.FotoBase64 = null;

                // CRÍTICO: Convertir FechaNacimiento a UTC si tiene valor
                if (cliente.FechaNacimiento.HasValue)
                {
                    cliente.FechaNacimiento = DateTime.SpecifyKind(cliente.FechaNacimiento.Value, DateTimeKind.Utc);
                }

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // Redirigir a la pantalla de captura de huella
                return RedirectToAction("CapturarHuella", new { id = cliente.Id });
            }

            // Si hay errores, mostrarlos en consola para debug
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                Console.WriteLine($"Error de validación: {error.ErrorMessage}");
            }

            return View(cliente);
        }

        // GET: /clientes/capturar-huella/{id}
        [HttpGet("capturar-huella/{id}")]
        public async Task<IActionResult> CapturarHuella(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            ViewBag.ClienteNombre = $"{cliente.Nombre} {cliente.Apellido}";
            return View(cliente);
        }

        // GET: /clientes/editar/{id}
        [HttpGet("editar/{id}")]
        public async Task<IActionResult> Editar(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();
            return View(cliente);
        }

        // POST: /clientes/editar/{id}
        [HttpPost("editar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Guid id, Cliente cliente)
        {
            if (id != cliente.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                var dbCliente = await _context.Clientes.FindAsync(id);
                if (dbCliente == null)
                    return NotFound();

                dbCliente.Nombre = cliente.Nombre;
                dbCliente.Apellido = cliente.Apellido;
                dbCliente.Cedula = cliente.Cedula;
                dbCliente.Telefono = cliente.Telefono;
                dbCliente.Email = cliente.Email;
                dbCliente.FechaNacimiento = cliente.FechaNacimiento;
                dbCliente.Direccion = cliente.Direccion;
                dbCliente.HuellaDigital = cliente.HuellaDigital;
                dbCliente.HuellaTemplate = cliente.HuellaTemplate;
                dbCliente.FotoBase64 = cliente.FotoBase64;
                dbCliente.Activo = cliente.Activo;
                dbCliente.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        // GET: /clientes/eliminar/{id}
        [HttpGet("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();
            return View(cliente);
        }

        // POST: /clientes/eliminar/{id}
        [HttpPost("eliminar/{id}"), ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}