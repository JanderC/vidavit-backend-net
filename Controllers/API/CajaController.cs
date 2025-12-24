using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class CajaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CajaController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtener balance de caja en un rango de fechas
        /// </summary>
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            try
            {
                var inicio = desde.HasValue
                    ? DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Utc)
                    : DateTime.UtcNow.Date;

                var fin = hasta.HasValue
                    ? DateTime.SpecifyKind(hasta.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc)
                    : DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= inicio && m.Fecha <= fin)
                    .ToListAsync();

                var ingresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balance = ingresos - egresos;

                // Desglose por categoría
                var ingresosPorCategoria = movimientos
                    .Where(m => m.Tipo == "ingreso")
                    .GroupBy(m => m.Categoria)
                    .Select(g => new { categoria = g.Key, total = g.Sum(m => m.Monto) })
                    .ToList();

                var egresosPorCategoria = movimientos
                    .Where(m => m.Tipo == "egreso")
                    .GroupBy(m => m.Categoria)
                    .Select(g => new { categoria = g.Key, total = g.Sum(m => m.Monto) })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    desde = inicio,
                    hasta = fin,
                    ingresos,
                    egresos,
                    balance,
                    ingresosPorCategoria,
                    egresosPorCategoria,
                    totalMovimientos = movimientos.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener balance",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar un ingreso en caja
        /// </summary>
        [HttpPost("ingreso")]
        public async Task<IActionResult> RegistrarIngreso([FromBody] JsonElement data)
        {
            try
            {
                var categoria = data.GetProperty("categoria").GetString();
                var monto = data.GetProperty("monto").GetDecimal();
                var descripcion = data.GetProperty("descripcion").GetString();
                var metodoPago = data.GetProperty("metodoPago").GetString();
                var usuarioId = Guid.Parse(data.GetProperty("usuarioId").GetString());

                Guid? referenciaId = null;
                if (data.TryGetProperty("referenciaId", out var refEl) && !string.IsNullOrWhiteSpace(refEl.GetString()))
                {
                    referenciaId = Guid.Parse(refEl.GetString());
                }

                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = categoria,
                    Monto = monto,
                    Descripcion = descripcion,
                    MetodoPago = metodoPago,
                    UsuarioId = usuarioId,
                    ReferenciaId = referenciaId,
                    Fecha = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Ingreso registrado correctamente",
                    data = movimiento
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar ingreso",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar un egreso en caja
        /// </summary>
        [HttpPost("egreso")]
        public async Task<IActionResult> RegistrarEgreso([FromBody] JsonElement data)
        {
            try
            {
                var categoria = data.GetProperty("categoria").GetString();
                var monto = data.GetProperty("monto").GetDecimal();
                var descripcion = data.GetProperty("descripcion").GetString();
                var metodoPago = data.GetProperty("metodoPago").GetString();
                var usuarioId = Guid.Parse(data.GetProperty("usuarioId").GetString());

                Guid? referenciaId = null;
                if (data.TryGetProperty("referenciaId", out var refEl) && !string.IsNullOrWhiteSpace(refEl.GetString()))
                {
                    referenciaId = Guid.Parse(refEl.GetString());
                }

                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "egreso",
                    Categoria = categoria,
                    Monto = monto,
                    Descripcion = descripcion,
                    MetodoPago = metodoPago,
                    UsuarioId = usuarioId,
                    ReferenciaId = referenciaId,
                    Fecha = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Egreso registrado correctamente",
                    data = movimiento
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar egreso",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener todos los movimientos de caja
        /// </summary>
        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] string tipo = null,
            [FromQuery] string categoria = null)
        {
            try
            {
                var inicio = desde.HasValue
                    ? DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Utc)
                    : DateTime.UtcNow.Date.AddDays(-30);

                var fin = hasta.HasValue
                    ? DateTime.SpecifyKind(hasta.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc)
                    : DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);

                var query = _context.MovimientosCaja
                    .Where(m => m.Fecha >= inicio && m.Fecha <= fin);

                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    query = query.Where(m => m.Tipo == tipo);
                }

                if (!string.IsNullOrWhiteSpace(categoria))
                {
                    query = query.Where(m => m.Categoria == categoria);
                }

                var movimientos = await query
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        m.Fecha,
                        m.ReferenciaId,
                        m.UsuarioId,
                        usuario = m.Usuario != null ? m.Usuario.Nombre : "Sistema"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    total = movimientos.Count,
                    data = movimientos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener movimientos del día actual
        /// </summary>
        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var manana = hoy.AddDays(1);

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= hoy && m.Fecha < manana)
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        m.Fecha,
                        usuario = m.Usuario != null ? m.Usuario.Nombre : "Sistema"
                    })
                    .ToListAsync();

                var ingresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    ingresos,
                    egresos,
                    balance = ingresos - egresos,
                    movimientos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos de hoy",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Eliminar un movimiento de caja
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarMovimiento(Guid id)
        {
            try
            {
                var movimiento = await _context.MovimientosCaja.FindAsync(id);
                if (movimiento == null)
                {
                    return NotFound(new { success = false, message = "Movimiento no encontrado" });
                }

                _context.MovimientosCaja.Remove(movimiento);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Movimiento eliminado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al eliminar movimiento",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener resumen de caja por período
        /// </summary>
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] string periodo = "mes")
        {
            try
            {
                DateTime inicio;
                DateTime fin = DateTime.UtcNow;

                switch (periodo.ToLower())
                {
                    case "dia":
                        inicio = DateTime.UtcNow.Date;
                        break;
                    case "semana":
                        inicio = DateTime.UtcNow.Date.AddDays(-7);
                        break;
                    case "mes":
                        inicio = DateTime.UtcNow.Date.AddMonths(-1);
                        break;
                    case "año":
                        inicio = DateTime.UtcNow.Date.AddYears(-1);
                        break;
                    default:
                        inicio = DateTime.UtcNow.Date.AddMonths(-1);
                        break;
                }

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= inicio && m.Fecha <= fin)
                    .ToListAsync();

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                // Top categorías de ingresos
                var topIngresos = movimientos
                    .Where(m => m.Tipo == "ingreso")
                    .GroupBy(m => m.Categoria)
                    .Select(g => new { categoria = g.Key, total = g.Sum(m => m.Monto), cantidad = g.Count() })
                    .OrderByDescending(x => x.total)
                    .Take(5)
                    .ToList();

                // Top categorías de egresos
                var topEgresos = movimientos
                    .Where(m => m.Tipo == "egreso")
                    .GroupBy(m => m.Categoria)
                    .Select(g => new { categoria = g.Key, total = g.Sum(m => m.Monto), cantidad = g.Count() })
                    .OrderByDescending(x => x.total)
                    .Take(5)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    periodo,
                    desde = inicio,
                    hasta = fin,
                    totalIngresos,
                    totalEgresos,
                    balance = totalIngresos - totalEgresos,
                    topIngresos,
                    topEgresos,
                    totalMovimientos = movimientos.Count
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