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

        // GET: /productos/eliminar/{id}
        [HttpGet("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(Guid id)
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return NotFound();

            var tieneVentas = await _context.VentasProductos
                .AnyAsync(v => v.ProductoId == id);

            if (tieneVentas)
            {
                TempData["Error"] = "No se puede eliminar el producto porque tiene ventas asociadas.";
                return RedirectToAction(nameof(Index));
            }

            return View(producto);
        }

        // POST: /productos/eliminar/{id}
        [HttpPost("eliminar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            var tieneVentas = await _context.VentasProductos
                .AnyAsync(v => v.ProductoId == id);

            if (tieneVentas)
            {
                TempData["Error"] = "No se puede eliminar el producto porque tiene ventas asociadas.";
                return RedirectToAction(nameof(Index));
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Producto eliminado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /productos/vender
        [HttpGet("vender")]
        public IActionResult Vender()
        {
            return View();
        }

        // GET: /productos/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<IActionResult> CuentasPendientes()
        {
            var ventasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente")
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View(ventasPendientes);
        }
    }
}