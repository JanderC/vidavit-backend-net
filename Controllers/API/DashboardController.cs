using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VidaFit.Controllers.API
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

        // GET: api/Dashboard/estadisticas
        [HttpGet("estadisticas")]
        public async Task<IActionResult> GetEstadisticas()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var finMes = inicioMes.AddMonths(1).AddTicks(-1);
                var sieteDias = hoy.AddDays(7);

                var totalClientes = await _context.Clientes.CountAsync(c => c.Activo);
                var checkInsHoy = await _context.CheckIns.CountAsync(ci => ci.FechaHora.Date == hoy);
                var membresiasPorVencer = await _context.Membresias
                    .CountAsync(m => m.Estado == "activa" && m.FechaVencimiento >= hoy && m.FechaVencimiento <= sieteDias);
                var membresiasVencidas = await _context.Membresias
                    .Where(m => m.Estado == "vencida")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .Take(10)
                    .ToListAsync();

                // Detalles de membresías próximas a vencer
                var detalleProximasVencer = await _context.Membresias
                    .Where(m => m.Estado == "activa" && m.FechaVencimiento >= hoy && m.FechaVencimiento <= sieteDias)
                    .OrderBy(m => m.FechaVencimiento)
                    .Join(_context.Clientes, m => m.ClienteId, c => c.Id, (m, c) => new { m, c })
                    .Join(_context.Planes, mc => mc.m.PlanId, p => p.Id, (mc, p) => new
                    {
                        clienteNombre = mc.c.Nombre + " " + mc.c.Apellido,
                        planNombre = p.Nombre,
                        fechaVencimiento = mc.m.FechaVencimiento,
                        diasRestantes = (mc.m.FechaVencimiento - hoy).Days
                    })
                    .ToListAsync();

                // Detalles de membresías vencidas
                var detalleVencidas = await _context.Membresias
                    .Where(m => m.Estado == "vencida")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .Take(10)
                    .Join(_context.Clientes, m => m.ClienteId, c => c.Id, (m, c) => new { m, c })
                    .Join(_context.Planes, mc => mc.m.PlanId, p => p.Id, (mc, p) => new
                    {
                        clienteNombre = mc.c.Nombre + " " + mc.c.Apellido,
                        planNombre = p.Nombre,
                        fechaVencimiento = mc.m.FechaVencimiento,
                        diasVencido = (hoy - mc.m.FechaVencimiento).Days
                    })
                    .ToListAsync();

                // Productos pendientes de pago
                var productosPendientes = await _context.VentasProductos
                    .Where(v => v.EstadoPago == "pendiente")
                    .SumAsync(v => (decimal?)v.Total) ?? 0;

                // Top clientes del mes
                var topClientes = await _context.CheckIns
                    .Where(ci => ci.FechaHora >= inicioMes && ci.FechaHora <= finMes)
                    .GroupBy(ci => ci.ClienteId)
                    .Select(g => new
                    {
                        ClienteId = g.Key,
                        TotalCheckIns = g.Count()
                    })
                    .OrderByDescending(x => x.TotalCheckIns)
                    .Take(10)
                    .Join(_context.Clientes, x => x.ClienteId, c => c.Id, (x, c) => new
                    {
                        c.Id,
                        nombre = c.Nombre + " " + c.Apellido,
                        totalCheckIns = x.TotalCheckIns
                    })
                    .ToListAsync();

                // Asistencia últimos 30 días
                var asistencia30dias = await _context.CheckIns
                    .Where(ci => ci.FechaHora >= hoy.AddDays(-29))
                    .GroupBy(ci => ci.FechaHora.Date)
                    .Select(g => new { Fecha = g.Key, Total = g.Count() })
                    .OrderBy(g => g.Fecha)
                    .ToListAsync();

                // INGRESOS DEL MES DESGLOSADOS
                var movimientosMes = await _context.MovimientosCaja
                    .Where(m => m.Tipo == "ingreso" && m.Fecha >= inicioMes && m.Fecha <= finMes)
                    .ToListAsync();

                var ingresoMembresias = movimientosMes
                    .Where(m => m.Categoria == "membresia" || m.Categoria == "renovacion")
                    .Sum(m => m.Monto);

                var ingresoProductos = movimientosMes
                    .Where(m => m.Categoria == "producto")
                    .Sum(m => m.Monto);

                var ingresoAbonos = movimientosMes
                    .Where(m => m.Categoria == "abono_deuda")
                    .Sum(m => m.Monto);

                var ingresoOtros = movimientosMes
                    .Where(m => m.Categoria != "membresia" && m.Categoria != "renovacion" && m.Categoria != "producto" && m.Categoria != "abono_deuda")
                    .Sum(m => m.Monto);

                var totalIngresosMes = ingresoMembresias + ingresoProductos + ingresoAbonos + ingresoOtros;

                return Ok(new
                {
                    resumen = new
                    {
                        totalClientes,
                        checkInsHoy,
                        membresiasPorVencer,
                        membresiasVencidas = membresiasVencidas.Count,
                        productosPendientes,
                        ingresosMes = new
                        {
                            total = totalIngresosMes,
                            membresias = ingresoMembresias,
                            productos = ingresoProductos,
                            abonos = ingresoAbonos,
                            otros = ingresoOtros
                        }
                    },
                    alertas = new
                    {
                        membresiasPorVencer,
                        membresiasVencidas = membresiasVencidas.Count,
                        detalleProximasVencer,
                        detalleVencidas
                    },
                    topClientes,
                    asistencia30dias
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

        // GET: api/Dashboard/resumen (mantener compatibilidad)
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var sieteDias = hoy.AddDays(7);

                var totalClientes = await _context.Clientes.CountAsync(c => c.Activo);
                var checkInsHoy = await _context.CheckIns.CountAsync(ci => ci.FechaHora.Date == hoy);
                var membresiasPorVencer = await _context.Membresias
                    .CountAsync(m => m.Estado == "activa" && m.FechaVencimiento >= hoy && m.FechaVencimiento <= sieteDias);
                var membresiasVencidas = await _context.Membresias
                    .Where(m => m.Estado == "vencida")
                    .OrderByDescending(m => m.FechaVencimiento)
                    .Take(10)
                    .ToListAsync();
                var productosPendientes = await _context.VentasProductos
                    .Where(v => v.EstadoPago == "pendiente")
                    .SumAsync(v => (decimal?)v.Total) ?? 0;
                var topClientes = await _context.CheckIns
                    .GroupBy(ci => ci.ClienteId)
                    .Select(g => new
                    {
                        ClienteId = g.Key,
                        TotalCheckIns = g.Count()
                    })
                    .OrderByDescending(x => x.TotalCheckIns)
                    .Take(10)
                    .Join(_context.Clientes, x => x.ClienteId, c => c.Id, (x, c) => new
                    {
                        c.Id,
                        Nombre = c.Nombre + " " + c.Apellido,
                        x.TotalCheckIns
                    })
                    .ToListAsync();

                var asistencia30dias = await _context.CheckIns
                    .Where(ci => ci.FechaHora >= hoy.AddDays(-29))
                    .GroupBy(ci => ci.FechaHora.Date)
                    .Select(g => new { Fecha = g.Key, Total = g.Count() })
                    .OrderBy(g => g.Fecha)
                    .ToListAsync();

                return Ok(new
                {
                    totalClientes,
                    checkInsHoy,
                    membresiasPorVencer,
                    membresiasVencidas,
                    productosPendientes,
                    topClientes,
                    asistencia30dias
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener resumen",
                    error = ex.Message
                });
            }
        }
    }
}