using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.WEB
{
    [Route("membresias/planes")]
    public class PlanesController : Controller
    {
        private readonly AppDbContext _context;

        public PlanesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /membresias/planes
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var planes = await _context.Planes
                .OrderByDescending(p => p.Activo)
                .ThenBy(p => p.Nombre)
                .ToListAsync();
            return View(planes);
        }

        // GET: /membresias/planes/crear
        [HttpGet("crear")]
        public IActionResult Crear()
        {
            return View();
        }

        // GET: /membresias/planes/editar/{id}
        [HttpGet("editar/{id}")]
        public async Task<IActionResult> Editar(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound();
            return View(plan);
        }
    }
}