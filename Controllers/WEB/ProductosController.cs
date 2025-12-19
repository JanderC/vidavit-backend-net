using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.WEB
{
    [Route("productos")]
    public class ProductosController : Controller
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /productos
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var productos = await _context.Productos
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(productos);
        }

        // GET: /productos/detalle/{id}
        [HttpGet("detalle/{id}")]
        public async Task<IActionResult> Detalle(Guid id)
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return NotFound();

            return View(producto);
        }

        // GET: /productos/crear
        [HttpGet("crear")]
        public IActionResult Crear()
        {
            return View();
        }

        // GET: /productos/editar/{id}
        [HttpGet("editar/{id}")]
        public async Task<IActionResult> Editar(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();
            return View(producto);
        }

        // NOTA: Los métodos Vender() y CuentasPendientes() fueron movidos a VentasProductosController
        // para evitar conflictos de routing. Ahora las rutas correctas son:
        // - /ventas-productos/vender
        // - /ventas-productos/cuentas-pendientes

        // NOTA: El método Eliminar(id) fue removido porque ahora se elimina directamente
        // desde el Index usando JavaScript y el API REST (DELETE /api/Productos/{id})
    }
}