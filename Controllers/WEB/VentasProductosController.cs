using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.WEB
{

    [Route("ventas-productos")]
    public class VentasProductosController : Controller
    {
        private readonly AppDbContext _context;

        public VentasProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /ventas-productos
        [HttpGet("")]
        public async Task<IActionResult> Index(Guid? clienteId, string estado = "todos")
        {
            var query = _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .AsQueryable();

            // Filtrar por cliente si se proporciona
            if (clienteId.HasValue)
            {
                query = query.Where(v => v.ClienteId == clienteId.Value);
                var cliente = await _context.Clientes.FindAsync(clienteId.Value);
                ViewBag.ClienteNombre = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}" : "";
                ViewBag.ClienteId = clienteId.Value;
            }

            // Filtrar por estado
            if (estado != "todos")
            {
                query = query.Where(v => v.EstadoPago == estado);
            }

            var ventas = await query
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            ViewBag.EstadoFiltro = estado;
            return View(ventas);
        }

        // GET: /ventas-productos/detalle/{id}
        [HttpGet("detalle/{id}")]
        public async Task<IActionResult> Detalle(Guid id)
        {
            var venta = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            return View(venta);
        }

        // GET: /ventas-productos/crear
        [HttpGet("crear")]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Productos = await _context.Productos.Where(p => p.Activo).ToListAsync();
            return View();
        }

        // POST: /ventas-productos/crear
        [HttpPost("crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(VentaProducto venta)
        {
            if (ModelState.IsValid)
            {
                venta.Id = Guid.NewGuid();
                venta.CreatedAt = DateTime.Now;
                venta.UpdatedAt = DateTime.Now;
                venta.FechaVenta = DateTime.Now;

                _context.VentasProductos.Add(venta);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Productos = await _context.Productos.Where(p => p.Activo).ToListAsync();
            return View(venta);
        }

        // POST: /ventas-productos/marcar-pagado/{id}
        [HttpPost("marcar-pagado/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarPagado(Guid id)
        {
            var venta = await _context.VentasProductos.FindAsync(id);
            if (venta == null)
                return NotFound();

            venta.EstadoPago = "pagado";
            venta.FechaPago = DateTime.Now;
            venta.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /ventas-productos/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<IActionResult> CuentasPendientes()
        {
            // Obtener todas las ventas pendientes agrupadas por cliente
            var ventasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente")
                .ToListAsync();

            // Agrupar por cliente y calcular totales
            var cuentasPendientes = ventasPendientes
                .GroupBy(v => v.Cliente)
                .Select(g => new CuentaPendienteReporte
                {
                    Cliente = g.Key,
                    TotalPendiente = g.Sum(v => v.Total),
                    Productos = g.Select(v => v.Producto.Nombre).Distinct().ToList()
                })
                .OrderByDescending(c => c.TotalPendiente)
                .ToList();

            return View(cuentasPendientes);
        }

        // GET: /ventas-productos/vender
        [HttpGet("vender")]
        public IActionResult Vender()
        {
            return View();
        }
    }
}