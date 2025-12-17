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

        // GET: /clientes/detalle/{id} o /clientes/details/{id}
        [HttpGet("detalle/{id}")]
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Detalle(Guid id)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                    .ThenInclude(m => m.Plan)
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
            // Remover errores de validación de campos opcionales y de navegación
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
                // Verificar si ya existe un cliente con esa cédula
                var existente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Cedula == cliente.Cedula);

                if (existente != null)
                {
                    ModelState.AddModelError("Cedula", "Ya existe un cliente con esta cédula");
                    return View(cliente);
                }

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
                    cliente.FechaNacimiento = DateTime.SpecifyKind(
                        cliente.FechaNacimiento.Value,
                        DateTimeKind.Utc
                    );
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
        [HttpGet("capturefingerprint/{id}")]
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
        [HttpGet("edit/{id}")]
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

            // Remover validación de colecciones de navegación
            ModelState.Remove("Membresias");
            ModelState.Remove("CheckIns");

            if (ModelState.IsValid)
            {
                var dbCliente = await _context.Clientes.FindAsync(id);
                if (dbCliente == null)
                    return NotFound();

                // Verificar si la cédula ya existe en otro cliente
                var cedulaExistente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Cedula == cliente.Cedula && c.Id != id);

                if (cedulaExistente != null)
                {
                    ModelState.AddModelError("Cedula", "Ya existe otro cliente con esta cédula");
                    return View(cliente);
                }

                dbCliente.Nombre = cliente.Nombre;
                dbCliente.Apellido = cliente.Apellido;
                dbCliente.Cedula = cliente.Cedula;
                dbCliente.Telefono = string.IsNullOrWhiteSpace(cliente.Telefono) ? null : cliente.Telefono;
                dbCliente.Email = string.IsNullOrWhiteSpace(cliente.Email) ? null : cliente.Email;
                dbCliente.Direccion = string.IsNullOrWhiteSpace(cliente.Direccion) ? null : cliente.Direccion;
                dbCliente.Activo = cliente.Activo;
                dbCliente.UpdatedAt = DateTime.UtcNow;

                // Actualizar fecha de nacimiento si viene
                if (cliente.FechaNacimiento.HasValue)
                {
                    dbCliente.FechaNacimiento = DateTime.SpecifyKind(
                        cliente.FechaNacimiento.Value,
                        DateTimeKind.Utc
                    );
                }

                // Actualizar foto solo si viene nueva
                if (!string.IsNullOrWhiteSpace(cliente.FotoBase64))
                {
                    dbCliente.FotoBase64 = cliente.FotoBase64;
                }

                // Actualizar huella solo si viene nueva
                if (!string.IsNullOrWhiteSpace(cliente.HuellaTemplate))
                {
                    dbCliente.HuellaTemplate = cliente.HuellaTemplate;
                }

                if (!string.IsNullOrWhiteSpace(cliente.HuellaDigital))
                {
                    dbCliente.HuellaDigital = cliente.HuellaDigital;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        // GET: /clientes/eliminar/{id}
        [HttpGet("eliminar/{id}")]
        [HttpGet("delete/{id}")]
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