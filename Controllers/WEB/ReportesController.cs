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

            var ingresos = await _context.Pagos
                .Where(p => p.FechaPago.Date >= fechaInicio && p.FechaPago.Date <= fechaFin)
                .GroupBy(p => p.FechaPago.Date)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = g.Sum(p => p.Monto)
                })
                .OrderBy(g => g.Fecha)
                .ToListAsync();

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

            var productos = await _context.VentasProductos
                .Where(v => v.FechaVenta.Date >= fechaInicio && v.FechaVenta.Date <= fechaFin && v.EstadoPago == "pagado")
                .GroupBy(v => v.ProductoId)
                .Select(g => new
                {
                    ProductoId = g.Key,
                    Nombre = g.First().Producto.Nombre,
                    Cantidad = g.Sum(v => v.Cantidad),
                    Total = g.Sum(v => v.Total)
                })
                .OrderByDescending(g => g.Cantidad)
                .Take(10)
                .ToListAsync();

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(productos);
        }

        // GET: /reportes/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<IActionResult> CuentasPendientes()
        {
            var pendientes = await _context.VentasProductos
                .Where(v => v.EstadoPago == "pendiente")
                .GroupBy(v => v.ClienteId)
                .Select(g => new
                {
                    Cliente = g.First().Cliente,
                    TotalPendiente = g.Sum(v => v.Total),
                    Productos = g.Select(v => v.Producto.Nombre).ToList()
                })
                .OrderByDescending(g => g.TotalPendiente)
                .ToListAsync();

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