using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VidaFit.Controllers.WEB
{
    // Clases simples para los reportes
    public class AsistenciaReporte
    {
        public DateTime Fecha { get; set; }
        public int Total { get; set; }
    }

    public class IngresoMembresiaReporte
    {
        public string PlanNombre { get; set; }
        public int CantidadPagos { get; set; }
        public decimal Total { get; set; }
    }

    public class ProductoVendidoReporte
    {
        public Guid ProductoId { get; set; }
        public string Nombre { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class CuentaPendienteReporte
    {
        public Cliente Cliente { get; set; }
        public decimal TotalPendiente { get; set; }
        public List<string> Productos { get; set; }
    }

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
            // Convertir fechas a UTC para PostgreSQL
            var fechaInicio = (desde ?? DateTime.Now.AddDays(-30));
            var fechaFin = (hasta ?? DateTime.Now);

            // Asegurar que las fechas sean UTC
            if (fechaInicio.Kind == DateTimeKind.Unspecified)
            {
                fechaInicio = DateTime.SpecifyKind(fechaInicio, DateTimeKind.Utc);
            }
            else if (fechaInicio.Kind == DateTimeKind.Local)
            {
                fechaInicio = fechaInicio.ToUniversalTime();
            }

            if (fechaFin.Kind == DateTimeKind.Unspecified)
            {
                fechaFin = DateTime.SpecifyKind(fechaFin, DateTimeKind.Utc);
            }
            else if (fechaFin.Kind == DateTimeKind.Local)
            {
                fechaFin = fechaFin.ToUniversalTime();
            }

            // Establecer inicio del día (00:00:00) y fin del día (23:59:59)
            fechaInicio = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            var asistencia = await _context.CheckIns
                .Where(c => c.FechaHora >= fechaInicio && c.FechaHora <= fechaFin)
                .GroupBy(c => c.FechaHora.Date)
                .Select(g => new AsistenciaReporte
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
            // Convertir fechas a UTC para PostgreSQL
            var fechaInicio = (desde ?? DateTime.Now.AddMonths(-1));
            var fechaFin = (hasta ?? DateTime.Now);

            // Asegurar que las fechas sean UTC
            if (fechaInicio.Kind == DateTimeKind.Unspecified)
            {
                fechaInicio = DateTime.SpecifyKind(fechaInicio, DateTimeKind.Utc);
            }
            else if (fechaInicio.Kind == DateTimeKind.Local)
            {
                fechaInicio = fechaInicio.ToUniversalTime();
            }

            if (fechaFin.Kind == DateTimeKind.Unspecified)
            {
                fechaFin = DateTime.SpecifyKind(fechaFin, DateTimeKind.Utc);
            }
            else if (fechaFin.Kind == DateTimeKind.Local)
            {
                fechaFin = fechaFin.ToUniversalTime();
            }

            // Establecer inicio del día (00:00:00) y fin del día (23:59:59)
            fechaInicio = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            // PASO 1: Cargar TODOS los datos con Include
            var pagos = await _context.Pagos
                .Include(p => p.Membresia)
                    .ThenInclude(m => m.Plan)
                .Where(p => p.FechaPago >= fechaInicio && p.FechaPago <= fechaFin)
                .ToListAsync();

            // PASO 2: Agrupar EN MEMORIA y crear objetos fuertemente tipados
            var ingresos = pagos
                .GroupBy(p => new { PlanId = p.Membresia.PlanId, PlanNombre = p.Membresia.Plan.Nombre })
                .Select(g => new IngresoMembresiaReporte
                {
                    PlanNombre = g.Key.PlanNombre,
                    CantidadPagos = g.Count(),
                    Total = g.Sum(p => p.Monto)
                })
                .OrderByDescending(i => i.Total)
                .ToList();

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(ingresos);
        }

        // GET: /reportes/productos-mas-vendidos
        [HttpGet("productos-mas-vendidos")]
        public async Task<IActionResult> ProductosMasVendidos(DateTime? desde, DateTime? hasta)
        {
            // Convertir fechas a UTC para PostgreSQL
            var fechaInicio = (desde ?? DateTime.Now.AddMonths(-1));
            var fechaFin = (hasta ?? DateTime.Now);

            // Asegurar que las fechas sean UTC y establecer hora inicio/fin del día
            if (fechaInicio.Kind == DateTimeKind.Unspecified)
            {
                fechaInicio = DateTime.SpecifyKind(fechaInicio, DateTimeKind.Utc);
            }
            else if (fechaInicio.Kind == DateTimeKind.Local)
            {
                fechaInicio = fechaInicio.ToUniversalTime();
            }

            if (fechaFin.Kind == DateTimeKind.Unspecified)
            {
                fechaFin = DateTime.SpecifyKind(fechaFin, DateTimeKind.Utc);
            }
            else if (fechaFin.Kind == DateTimeKind.Local)
            {
                fechaFin = fechaFin.ToUniversalTime();
            }

            // Establecer inicio del día (00:00:00) y fin del día (23:59:59)
            fechaInicio = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            // PASO 1: Cargar datos usando Select para evitar cargar entidades completas con ClienteId NULL
            // Esto evita el error cuando ClienteId es NULL porque no intentamos materializar la entidad Cliente
            var ventas = await _context.VentasProductos
                .Where(v => v.FechaVenta >= fechaInicio
                         && v.FechaVenta <= fechaFin
                         && v.EstadoPago == "pagado")
                .Select(v => new
                {
                    v.ProductoId,
                    ProductoNombre = v.Producto.Nombre,
                    v.Cantidad,
                    v.Total
                })
                .ToListAsync();

            // PASO 2: Agrupar EN MEMORIA y crear objetos fuertemente tipados
            var productos = ventas
                .GroupBy(v => new { v.ProductoId, v.ProductoNombre })
                .Select(g => new ProductoVendidoReporte
                {
                    ProductoId = g.Key.ProductoId,
                    Nombre = g.Key.ProductoNombre,
                    Cantidad = g.Sum(v => v.Cantidad),
                    Total = g.Sum(v => v.Total)
                })
                .OrderByDescending(g => g.Cantidad)
                .Take(10)
                .ToList();

            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            return View(productos);
        }

        // GET: /reportes/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<IActionResult> CuentasPendientes()
        {
            // PASO 1: Cargar TODO con Include, pero filtrar solo registros con ClienteId NO NULL
            // Esto previene el error de intentar cargar Cliente cuando ClienteId es NULL
            var ventasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente" && v.ClienteId != null)
                .ToListAsync();

            // PASO 2: Agrupar EN MEMORIA y crear objetos fuertemente tipados
            var pendientes = ventasPendientes
                .GroupBy(v => v.ClienteId)
                .Select(g => new CuentaPendienteReporte
                {
                    Cliente = g.First().Cliente,
                    TotalPendiente = g.Sum(v => v.Total),
                    Productos = g.Select(v => v.Producto.Nombre).Distinct().ToList()
                })
                .OrderByDescending(g => g.TotalPendiente)
                .ToList();

            return View(pendientes);
        }

        // GET: /reportes/clientes-inactivos
        [HttpGet("clientes-inactivos")]
        public async Task<IActionResult> ClientesInactivos(int dias = 30)
        {
            // Convertir fecha a UTC para PostgreSQL
            var fechaLimite = DateTime.Now.AddDays(-dias);

            // Asegurar que la fecha sea UTC
            if (fechaLimite.Kind == DateTimeKind.Unspecified)
            {
                fechaLimite = DateTime.SpecifyKind(fechaLimite, DateTimeKind.Utc);
            }
            else if (fechaLimite.Kind == DateTimeKind.Local)
            {
                fechaLimite = fechaLimite.ToUniversalTime();
            }

            // Establecer inicio del día (00:00:00)
            fechaLimite = new DateTime(fechaLimite.Year, fechaLimite.Month, fechaLimite.Day, 0, 0, 0, DateTimeKind.Utc);

            var inactivos = await _context.Clientes
                .Where(c => !c.CheckIns.Any(ci => ci.FechaHora >= fechaLimite) && c.Activo)
                .ToListAsync();

            ViewBag.Dias = dias;
            return View(inactivos);
        }
    }
}