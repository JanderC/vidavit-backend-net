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
        public async Task<IActionResult> GetBalance([FromQuery] string desde = null, [FromQuery] string hasta = null)
        {
            try
            {
                var todosMovimientos = await _context.MovimientosCaja.ToListAsync();

                var movimientos = todosMovimientos;

                if (!string.IsNullOrWhiteSpace(desde) && DateTime.TryParse(desde, out DateTime fechaDesde))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date >= fechaDesde.Date).ToList();
                }

                if (!string.IsNullOrWhiteSpace(hasta) && DateTime.TryParse(hasta, out DateTime fechaHasta))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date <= fechaHasta.Date).ToList();
                }

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
                    desde = desde,
                    hasta = hasta,
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
            [FromQuery] string desde = null,
            [FromQuery] string hasta = null,
            [FromQuery] string tipo = null,
            [FromQuery] string categoria = null)
        {
            try
            {
                var query = _context.MovimientosCaja.AsQueryable();

                // Aplicar filtros
                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    query = query.Where(m => m.Tipo == tipo);
                }

                if (!string.IsNullOrWhiteSpace(categoria))
                {
                    query = query.Where(m => m.Categoria == categoria);
                }

                // Traer todos los datos
                var todosMovimientos = await query.OrderByDescending(m => m.Fecha).ToListAsync();

                // Si no hay filtros de fecha, devolver todo
                if (string.IsNullOrWhiteSpace(desde) && string.IsNullOrWhiteSpace(hasta))
                {
                    var todosDto = todosMovimientos.Select(m => new
                    {
                        Id = m.Id,
                        Tipo = m.Tipo,
                        Categoria = m.Categoria,
                        Monto = m.Monto,
                        Descripcion = m.Descripcion,
                        MetodoPago = m.MetodoPago,
                        Fecha = m.Fecha,
                        ReferenciaId = m.ReferenciaId,
                        UsuarioId = m.UsuarioId,
                        usuario = "Sistema"
                    }).ToList();

                    return Ok(new
                    {
                        success = true,
                        total = todosDto.Count,
                        data = todosDto
                    });
                }

                // Filtrar por fechas
                var movimientosFiltrados = todosMovimientos;

                if (!string.IsNullOrWhiteSpace(desde))
                {
                    if (DateTime.TryParse(desde, out DateTime fechaDesde))
                    {
                        // Convertir fecha local a UTC para comparar
                        var desdeUtc = DateTime.SpecifyKind(fechaDesde.Date, DateTimeKind.Utc);
                        movimientosFiltrados = movimientosFiltrados
                            .Where(m => m.Fecha.Date >= desdeUtc.Date)
                            .ToList();
                    }
                }

                if (!string.IsNullOrWhiteSpace(hasta))
                {
                    if (DateTime.TryParse(hasta, out DateTime fechaHasta))
                    {
                        // Convertir fecha local a UTC y agregar hasta fin de día
                        var hastaUtc = DateTime.SpecifyKind(fechaHasta.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                        movimientosFiltrados = movimientosFiltrados
                            .Where(m => m.Fecha <= hastaUtc)
                            .ToList();
                    }
                }

                var movimientosDto = movimientosFiltrados.Select(m => new
                {
                    Id = m.Id,
                    Tipo = m.Tipo,
                    Categoria = m.Categoria,
                    Monto = m.Monto,
                    Descripcion = m.Descripcion,
                    MetodoPago = m.MetodoPago,
                    Fecha = m.Fecha,
                    ReferenciaId = m.ReferenciaId,
                    UsuarioId = m.UsuarioId,
                    usuario = "Sistema"
                }).ToList();

                return Ok(new
                {
                    success = true,
                    total = movimientosDto.Count,
                    data = movimientosDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos",
                    error = ex.Message,
                    stack = ex.StackTrace
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
                // Traer TODOS los movimientos sin filtro y sin JOIN con usuario
                var todosMovimientos = await _context.MovimientosCaja
                    .OrderByDescending(m => m.Fecha)
                    .ToListAsync();

                // Filtrar en C# por fecha de hoy
                var hoy = DateTime.Today;
                var movimientos = todosMovimientos
                    .Where(m => m.Fecha.Year == hoy.Year &&
                                m.Fecha.Month == hoy.Month &&
                                m.Fecha.Day == hoy.Day)
                    .ToList();

                var ingresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                var movimientosDto = movimientos.Select(m => new
                {
                    Id = m.Id,
                    Tipo = m.Tipo,
                    Categoria = m.Categoria,
                    Monto = m.Monto,
                    Descripcion = m.Descripcion,
                    MetodoPago = m.MetodoPago,
                    Fecha = m.Fecha,
                    usuario = "Sistema"
                }).ToList();

                return Ok(new
                {
                    success = true,
                    ingresos,
                    egresos,
                    balance = ingresos - egresos,
                    movimientos = movimientosDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos de hoy",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Obtener saldo del día anterior (efectivo en caja de cierre)
        /// </summary>
        [HttpGet("saldo-anterior")]
        public async Task<IActionResult> GetSaldoAnterior()
        {
            try
            {
                // Obtener fecha de ayer en UTC
                var hoyUtc = DateTime.UtcNow.Date;
                var ayerUtc = hoyUtc.AddDays(-1);

                // Obtener todos los movimientos hasta ayer (inclusive) en UTC
                var movimientosHastaAyer = await _context.MovimientosCaja
                    .Where(m => m.Fecha.Date <= ayerUtc.Date)
                    .ToListAsync();

                // Calcular solo movimientos en efectivo
                var ingresosEfectivo = movimientosHastaAyer
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivo = movimientosHastaAyer
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var saldoAnterior = ingresosEfectivo - egresosEfectivo;

                return Ok(new
                {
                    success = true,
                    fecha = ayerUtc,
                    saldoAnterior = saldoAnterior,
                    ingresosEfectivo = ingresosEfectivo,
                    egresosEfectivo = egresosEfectivo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener saldo anterior",
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
                DateTime fin = DateTime.Now;

                switch (periodo.ToLower())
                {
                    case "dia":
                        inicio = DateTime.Today;
                        break;
                    case "semana":
                        inicio = DateTime.Today.AddDays(-7);
                        break;
                    case "mes":
                        inicio = DateTime.Today.AddMonths(-1);
                        break;
                    case "año":
                        inicio = DateTime.Today.AddYears(-1);
                        break;
                    default:
                        inicio = DateTime.Today.AddMonths(-1);
                        break;
                }

                var todosMovimientos = await _context.MovimientosCaja.ToListAsync();
                var movimientos = todosMovimientos
                    .Where(m => m.Fecha.Date >= inicio.Date && m.Fecha.Date <= fin.Date)
                    .ToList();

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