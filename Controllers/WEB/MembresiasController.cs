using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;


namespace VidaFit.Controllers.WEB
{
    [Route("membresias")]
    public class MembresiasController : Controller
    {
        private readonly AppDbContext _context;

        public MembresiasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /membresias
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var membresias = await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return View(membresias);
        }

        // GET: /membresias/detalle/{id}
        [HttpGet("detalle/{id}")]
        public async Task<IActionResult> Detalle(Guid id)
        {
            var membresia = await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (membresia == null)
                return NotFound();

            return View(membresia);
        }

        // GET: /membresias/crear
        [HttpGet("crear")]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Planes = await _context.Planes.Where(p => p.Activo).ToListAsync();
            return View();
        }

        // POST: /membresias/crear
        [HttpPost("crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Membresia membresia)
        {
            if (ModelState.IsValid)
            {
                membresia.Id = Guid.NewGuid();
                membresia.CreatedAt = DateTime.UtcNow;
                membresia.UpdatedAt = DateTime.UtcNow;
                membresia.Estado = "activa";
                _context.Membresias.Add(membresia);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Planes = await _context.Planes.Where(p => p.Activo).ToListAsync();
            return View(membresia);
        }

        // GET: /membresias/editar/{id}
        [HttpGet("editar/{id}")]
        public async Task<IActionResult> Editar(Guid id)
        {
            var membresia = await _context.Membresias.FindAsync(id);
            if (membresia == null)
                return NotFound();

            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Planes = await _context.Planes.Where(p => p.Activo).ToListAsync();
            return View(membresia);
        }

        // POST: /membresias/editar/{id}
        [HttpPost("editar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Guid id, Membresia membresia)
        {
            if (id != membresia.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                var dbMembresia = await _context.Membresias.FindAsync(id);
                if (dbMembresia == null)
                    return NotFound();

                dbMembresia.ClienteId = membresia.ClienteId;
                dbMembresia.PlanId = membresia.PlanId;
                dbMembresia.FechaInicio = membresia.FechaInicio;
                dbMembresia.FechaVencimiento = membresia.FechaVencimiento;
                dbMembresia.Estado = membresia.Estado;
                dbMembresia.MontoPagado = membresia.MontoPagado;
                dbMembresia.MetodoPago = membresia.MetodoPago;
                dbMembresia.Notas = membresia.Notas;
                dbMembresia.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Planes = await _context.Planes.Where(p => p.Activo).ToListAsync();
            return View(membresia);
        }

        // GET: /membresias/eliminar/{id}
        [HttpGet("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(Guid id)
        {
            var membresia = await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (membresia == null)
                return NotFound();

            return View(membresia);
        }

        // POST: /membresias/eliminar/{id}
        [HttpPost("eliminar/{id}"), ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(Guid id)
        {
            var membresia = await _context.Membresias.FindAsync(id);
            if (membresia == null)
                return NotFound();

            _context.Membresias.Remove(membresia);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}