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

        // POST: /productos/crear
        [HttpPost("crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Producto producto)
        {
            if (ModelState.IsValid)
            {
                producto.Id = Guid.NewGuid();
                producto.CreatedAt = DateTime.UtcNow;
                producto.UpdatedAt = DateTime.UtcNow;
                producto.Activo = true;
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
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

        // POST: /productos/editar/{id}
        [HttpPost("editar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Guid id, Producto producto)
        {
            if (id != producto.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                var dbProducto = await _context.Productos.FindAsync(id);
                if (dbProducto == null)
                    return NotFound();

                dbProducto.Nombre = producto.Nombre;
                dbProducto.Descripcion = producto.Descripcion;
                dbProducto.Precio = producto.Precio;
                dbProducto.Stock = producto.Stock;
                dbProducto.ImagenBase64 = producto.ImagenBase64;
                dbProducto.Categoria = producto.Categoria;
                dbProducto.Activo = producto.Activo;
                dbProducto.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // GET: /productos/eliminar/{id}
        [HttpGet("eliminar/{id}")]
        public async Task<IActionResult> Eliminar(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();
            return View(producto);
        }

        // POST: /productos/eliminar/{id}
        [HttpPost("eliminar/{id}"), ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /productos/vender
        [HttpGet("vender")]
        public IActionResult Vender()
        {
            return View();
        }

        [HttpGet("cuentas-pendientes")]
        public IActionResult CuentasPendientes()
        {
            return View();
        }
    }
}