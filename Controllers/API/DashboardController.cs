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

        // GET: api/Dashboard/resumen
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen()
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
    }
}