using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VidaFit.Controllers.WEB
{
    [Route("reportes")]
    public class ReportesController : Controller
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /reportes/asistencia
        [HttpGet("asistencia")]
        public async Task<IActionResult> Asistencia(DateTime? desde, DateTime? hasta)
        {
            var fechaInicio = desde ?? DateTime.UtcNow.AddDays(-30).Date;
            var fechaFin = hasta ?? DateTime.UtcNow.Date;

            var asistencia = await _context.CheckIns
                .Where(c => c.FechaHora.Date >= fechaInicio && c.FechaHora.Date <= fechaFin)
                .GroupBy(c => c.FechaHora.Date)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = g.Count()
                })
                .OrderBy(g => g.Fecha)
                .ToListAsync();

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(asistencia);
        }

        // GET: /reportes/ingresos-membresias
        [HttpGet("ingresos-membresias")]
        public async Task<IActionResult> IngresosMembresias(DateTime? desde, DateTime? hasta)
        {
            var fechaInicio = desde ?? DateTime.UtcNow.AddMonths(-1).Date;
            var fechaFin = hasta ?? DateTime.UtcNow.Date;

            // PASO 1: Cargar TODOS los datos con Include
            var pagos = await _context.Pagos
                .Include(p => p.Membresia)
                    .ThenInclude(m => m.Plan)
                .Where(p => p.FechaPago.Date >= fechaInicio && p.FechaPago.Date <= fechaFin)
                .ToListAsync(); // ← IMPORTANTE: ToListAsync() primero

            // PASO 2: Agrupar EN MEMORIA (después del ToListAsync)
            var ingresos = pagos
                .GroupBy(p => new { PlanId = p.Membresia.PlanId, PlanNombre = p.Membresia.Plan.Nombre })
                .Select(g => new
                {
                    PlanNombre = g.Key.PlanNombre,
                    CantidadPagos = g.Count(),
                    Total = g.Sum(p => p.Monto)
                })
                .OrderByDescending(i => i.Total)
                .ToList(); // ← ToList() otra vez para materializar el resultado

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(ingresos);
        }

        // GET: /reportes/productos-mas-vendidos
        [HttpGet("productos-mas-vendidos")]
        public async Task<IActionResult> ProductosMasVendidos(DateTime? desde, DateTime? hasta)
        {
            var fechaInicio = desde ?? DateTime.UtcNow.AddMonths(-1).Date;
            var fechaFin = hasta ?? DateTime.UtcNow.Date;

            // PASO 1: Cargar TODO con Include
            var ventas = await _context.VentasProductos
                .Include(v => v.Producto)
                .Where(v => v.FechaVenta.Date >= fechaInicio
                         && v.FechaVenta.Date <= fechaFin
                         && v.EstadoPago == "pagado")
                .ToListAsync(); // ← IMPORTANTE: ToListAsync() primero

            // PASO 2: Agrupar EN MEMORIA
            var productos = ventas
                .GroupBy(v => new { ProductoId = v.ProductoId, Nombre = v.Producto.Nombre })
                .Select(g => new
                {
                    ProductoId = g.Key.ProductoId,
                    Nombre = g.Key.Nombre,
                    Cantidad = g.Sum(v => v.Cantidad),
                    Total = g.Sum(v => v.Total)
                })
                .OrderByDescending(g => g.Cantidad)
                .Take(10)
                .ToList(); // ← ToList() para materializar

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(productos);
        }

        // GET: /reportes/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<IActionResult> CuentasPendientes()
        {
            // PASO 1: Cargar TODO con Include
            var ventasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente")
                .ToListAsync(); // ← IMPORTANTE: ToListAsync() primero

            // PASO 2: Agrupar EN MEMORIA
            var pendientes = ventasPendientes
                .GroupBy(v => v.ClienteId)
                .Select(g => new
                {
                    Cliente = g.First().Cliente,
                    TotalPendiente = g.Sum(v => v.Total),
                    Productos = g.Select(v => v.Producto.Nombre).Distinct().ToList()
                })
                .OrderByDescending(g => g.TotalPendiente)
                .ToList(); // ← ToList() para materializar

            return View(pendientes);
        }

        // GET: /reportes/clientes-inactivos
        [HttpGet("clientes-inactivos")]
        public async Task<IActionResult> ClientesInactivos(int dias = 30)
        {
            var fechaLimite = DateTime.UtcNow.AddDays(-dias).Date;

            var inactivos = await _context.Clientes
                .Where(c => !c.CheckIns.Any(ci => ci.FechaHora.Date >= fechaLimite) && c.Activo)
                .ToListAsync();

            ViewBag.Dias = dias;
            return View(inactivos);
        }
    }
}