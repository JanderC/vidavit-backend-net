using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;

namespace VidaFit.Controllers.WEB
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("estadisticas")]
        public async Task<IActionResult> GetEstadisticas()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var dentroSieteDias = hoy.AddDays(7);

                // RESUMEN
                var totalClientes = await _context.Clientes.CountAsync(c => c.Activo);
                var checkInsHoy = await _context.CheckIns.CountAsync(c => c.FechaHora.Date == hoy);
                var membresiasActivas = await _context.Membresias.CountAsync(m => m.Estado == "activa");
                var ingresosMes = await _context.Pagos
                    .Where(p => p.FechaPago >= inicioMes)
                    .SumAsync(p => (decimal?)p.Monto) ?? 0;

                var clientesConMembresia = await _context.Clientes
                    .CountAsync(c => c.Activo && c.Membresias.Any(m => m.Estado == "activa"));

                var productosPendientes = await _context.VentasProductos
                    .Where(v => v.EstadoPago == "pendiente")
                    .SumAsync(v => (decimal?)v.Total) ?? 0;

                // ALERTAS - Membresías próximas a vencer (7 días)
                var membresiasProximasVencer = await _context.Membresias
                    .Include(m => m.Cliente)
                    .Include(m => m.Plan)
                    .Where(m => m.Estado == "activa"
                        && m.FechaVencimiento >= hoy
                        && m.FechaVencimiento <= dentroSieteDias)
                    .Select(m => new
                    {
                        clienteNombre = m.Cliente.Nombre + " " + m.Cliente.Apellido,
                        planNombre = m.Plan.Nombre,
                        diasRestantes = (m.FechaVencimiento - hoy).Days
                    })
                    .ToListAsync();

                // ALERTAS - Membresías vencidas
                var membresiasVencidas = await _context.Membresias
                    .Include(m => m.Cliente)
                    .Include(m => m.Plan)
                    .Where(m => m.Estado == "vencida" || (m.Estado == "activa" && m.FechaVencimiento < hoy))
                    .Select(m => new
                    {
                        clienteNombre = m.Cliente.Nombre + " " + m.Cliente.Apellido,
                        planNombre = m.Plan.Nombre,
                        diasVencido = (hoy - m.FechaVencimiento).Days
                    })
                    .ToListAsync();

                // TOP CLIENTES - Los 10 más activos del mes
                var topClientes = await _context.CheckIns
                    .Where(c => c.FechaHora >= inicioMes)
                    .GroupBy(c => c.ClienteId)
                    .Select(g => new
                    {
                        clienteId = g.Key,
                        totalCheckIns = g.Count()
                    })
                    .OrderByDescending(x => x.totalCheckIns)
                    .Take(10)
                    .ToListAsync();

                var topClientesConNombres = new List<object>();
                foreach (var top in topClientes)
                {
                    var cliente = await _context.Clientes.FindAsync(top.clienteId);
                    if (cliente != null)
                    {
                        topClientesConNombres.Add(new
                        {
                            nombre = $"{cliente.Nombre} {cliente.Apellido}",
                            totalCheckIns = top.totalCheckIns
                        });
                    }
                }

                return Ok(new
                {
                    resumen = new
                    {
                        totalClientes,
                        checkInsHoy,
                        membresiasActivas,
                        ingresosMes,
                        clientesConMembresia,
                        productosPendientes
                    },
                    alertas = new
                    {
                        membresiasProximasVencer = membresiasProximasVencer.Count,
                        detalleProximasVencer = membresiasProximasVencer,
                        membresiasVencidas = membresiasVencidas.Count,
                        detalleVencidas = membresiasVencidas
                    },
                    topClientes = topClientesConNombres
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener estadísticas",
                    error = ex.Message
                });
            }
        }

        // NUEVO: Endpoint para ingresos mensuales (usado por Ingresos.cshtml)
        [HttpGet("ingresos")]
        public async Task<IActionResult> GetIngresos([FromQuery] int meses = 6)
        {
            try
            {
                var fechaInicio = DateTime.UtcNow.AddMonths(-meses).Date;

                var ingresos = await _context.Pagos
                    .Where(p => p.FechaPago >= fechaInicio)
                    .GroupBy(p => new { p.FechaPago.Year, p.FechaPago.Month })
                    .Select(g => new
                    {
                        año = g.Key.Year,
                        mes = g.Key.Month,
                        total = g.Sum(p => p.Monto)
                    })
                    .OrderBy(i => i.año)
                    .ThenBy(i => i.mes)
                    .ToListAsync();

                return Ok(ingresos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener ingresos",
                    error = ex.Message
                });
            }
        }

        // NUEVO: Endpoint para productos más vendidos (usado por Ingresos.cshtml)
        [HttpGet("productos-mas-vendidos")]
        public async Task<IActionResult> GetProductosMasVendidos([FromQuery] int limit = 5)
        {
            try
            {
                var ventas = await _context.VentasProductos
                    .Include(v => v.Producto)
                    .Where(v => v.EstadoPago == "pagado")
                    .ToListAsync();

                var productos = ventas
                    .GroupBy(v => new { v.ProductoId, v.Producto.Nombre })
                    .Select(g => new
                    {
                        productoNombre = g.Key.Nombre,
                        cantidadVendida = g.Sum(v => v.Cantidad),
                        totalIngresos = g.Sum(v => v.Total)
                    })
                    .OrderByDescending(p => p.cantidadVendida)
                    .Take(limit)
                    .ToList();

                return Ok(productos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener productos más vendidos",
                    error = ex.Message
                });
            }
        }
    }
}