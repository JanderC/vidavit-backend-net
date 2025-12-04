using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;


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
            if (ModelState.IsValid)
            {
                cliente.Id = Guid.NewGuid();
                cliente.CreatedAt = DateTime.UtcNow;
                cliente.UpdatedAt = DateTime.UtcNow;
                cliente.Activo = true;
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
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