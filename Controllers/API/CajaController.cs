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
        /// Obtener el ID del primer usuario activo del sistema
        /// </summary>
        private async Task<Guid> ObtenerUsuarioSistemaAsync()
        {
            var usuario = await _context.Usuarios
                .Where(u => u.Activo)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (usuario == Guid.Empty)
            {
                throw new Exception("No hay usuarios activos en el sistema");
            }

            return usuario;
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

                // Usuario único del sistema: admin@taurogym.com
                Guid usuarioId = await ObtenerUsuarioSistemaAsync();

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
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
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

                // Usuario único del sistema: admin@taurogym.com
                Guid usuarioId = await ObtenerUsuarioSistemaAsync();

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

                var todosMovimientos = await query.OrderByDescending(m => m.Fecha).ToListAsync();
                var movimientos = todosMovimientos;

                if (!string.IsNullOrWhiteSpace(desde) && DateTime.TryParse(desde, out DateTime fechaDesde))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date >= fechaDesde.Date).ToList();
                }

                if (!string.IsNullOrWhiteSpace(hasta) && DateTime.TryParse(hasta, out DateTime fechaHasta))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date <= fechaHasta.Date).ToList();
                }

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
                    movimientos = movimientosDto,
                    total = movimientos.Count
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
        /// Obtener movimientos de hoy con efectivo actual (solo después del último cierre)
        /// </summary>
        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                // Buscar el último cierre de caja
                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .FirstOrDefaultAsync();

                // Especificar UTC para evitar errores con PostgreSQL
                DateTime fechaDesde;
                if (ultimoCierre != null)
                {
                    fechaDesde = DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
                }
                else
                {
                    fechaDesde = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }

                // Solo movimientos DESPUÉS del último cierre
                var movimientosHoy = await _context.MovimientosCaja
                    .Where(m => m.Fecha > fechaDesde)
                    .OrderByDescending(m => m.Fecha)
                    .ToListAsync();

                // Calcular montos por método de pago
                var ingresosEfectivo = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                var totalIngresos = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                var movimientosDto = movimientosHoy.Select(m => new
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
                    fecha = DateTime.UtcNow.Date,
                    ingresos = totalIngresos,
                    egresos = totalEgresos,
                    balance = totalIngresos - totalEgresos,
                    ingresosEfectivo,
                    egresosEfectivo,
                    ingresosTransferencia,
                    egresosTransferencia,
                    movimientos = movimientosDto,
                    ultimoCierre = ultimoCierre?.FechaCierre
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
        /// Obtener saldo anterior (siempre 0 después de un cierre, para empezar de nuevo)
        /// </summary>
        [HttpGet("saldo-anterior")]
        public async Task<IActionResult> GetSaldoAnterior()
        {
            try
            {
                // Buscar el último cierre de caja
                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .FirstOrDefaultAsync();

                if (ultimoCierre != null)
                {
                    return Ok(new
                    {
                        success = true,
                        fechaCierre = ultimoCierre.FechaCierre,
                        saldoAnterior = 0, // SIEMPRE 0 después de cerrar
                        ultimoCierre = new
                        {
                            id = ultimoCierre.Id,
                            fecha = ultimoCierre.FechaCierre,
                            tipo = ultimoCierre.TipoCierre,
                            efectivoFinal = ultimoCierre.EfectivoFinal,
                            balanceGeneral = ultimoCierre.BalanceGeneral,
                            observaciones = ultimoCierre.Observaciones
                        }
                    });
                }

                // Si no hay cierres previos, también retornar 0
                return Ok(new
                {
                    success = true,
                    fechaCierre = (DateTime?)null,
                    saldoAnterior = 0,
                    ultimoCierre = (object)null
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
        /// Realizar cierre de caja manual
        /// </summary>
        [HttpPost("cerrar-caja")]
        public async Task<IActionResult> CerrarCaja([FromBody] JsonElement data)
        {
            try
            {
                var observaciones = data.TryGetProperty("observaciones", out var obs) ? obs.GetString() : "";

                // Usuario único del sistema
                Guid usuarioId = await ObtenerUsuarioSistemaAsync();

                // Buscar el último cierre
                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .FirstOrDefaultAsync();

                // El saldo anterior siempre es 0 - empezamos de nuevo después de cada cierre
                decimal saldoAnterior = 0;

                // Obtener movimientos DESPUÉS del último cierre
                DateTime fechaDesde;
                if (ultimoCierre != null)
                {
                    fechaDesde = DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
                }
                else
                {
                    fechaDesde = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha > fechaDesde)
                    .ToListAsync();

                // Calcular montos
                var ingresosEfectivo = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                var efectivoFinal = saldoAnterior + ingresosEfectivo - egresosEfectivo;
                var balanceGeneral = totalIngresos - totalEgresos;

                var cierre = new CierreCaja
                {
                    Id = Guid.NewGuid(),
                    FechaCierre = DateTime.UtcNow,
                    TipoCierre = "manual",
                    EfectivoInicial = saldoAnterior,
                    IngresosEfectivo = ingresosEfectivo,
                    EgresosEfectivo = egresosEfectivo,
                    EfectivoFinal = efectivoFinal,
                    IngresosTransferencia = ingresosTransferencia,
                    EgresosTransferencia = egresosTransferencia,
                    TotalIngresos = totalIngresos,
                    TotalEgresos = totalEgresos,
                    BalanceGeneral = balanceGeneral,
                    CantidadMovimientos = movimientos.Count,
                    Observaciones = observaciones,
                    UsuarioId = usuarioId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CierresCaja.Add(cierre);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Caja cerrada exitosamente",
                    cierre = new
                    {
                        id = cierre.Id,
                        fechaCierre = cierre.FechaCierre,
                        tipo = cierre.TipoCierre,
                        efectivoInicial = cierre.EfectivoInicial,
                        ingresosEfectivo = cierre.IngresosEfectivo,
                        egresosEfectivo = cierre.EgresosEfectivo,
                        efectivoFinal = cierre.EfectivoFinal,
                        ingresosTransferencia = cierre.IngresosTransferencia,
                        egresosTransferencia = cierre.EgresosTransferencia,
                        totalIngresos = cierre.TotalIngresos,
                        totalEgresos = cierre.TotalEgresos,
                        balanceGeneral = cierre.BalanceGeneral,
                        cantidadMovimientos = cierre.CantidadMovimientos
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al cerrar caja",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Obtener historial de cierres de caja
        /// </summary>
        [HttpGet("historial-cierres")]
        public async Task<IActionResult> GetHistorialCierres([FromQuery] int limit = 10)
        {
            try
            {
                var cierres = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Take(limit)
                    .ToListAsync();

                var cierresDto = cierres.Select(c => new
                {
                    id = c.Id,
                    fechaCierre = c.FechaCierre,
                    tipo = c.TipoCierre,
                    efectivoInicial = c.EfectivoInicial,
                    efectivoFinal = c.EfectivoFinal,
                    ingresosEfectivo = c.IngresosEfectivo,
                    egresosEfectivo = c.EgresosEfectivo,
                    ingresosTransferencia = c.IngresosTransferencia,
                    egresosTransferencia = c.EgresosTransferencia,
                    totalIngresos = c.TotalIngresos,
                    totalEgresos = c.TotalEgresos,
                    balanceGeneral = c.BalanceGeneral,
                    cantidadMovimientos = c.CantidadMovimientos,
                    observaciones = c.Observaciones
                }).ToList();

                return Ok(new
                {
                    success = true,
                    cierres = cierresDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener historial de cierres",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener resumen semanal (Lunes a Sábado)
        /// </summary>
        [HttpGet("resumen-semanal")]
        public async Task<IActionResult> GetResumenSemanal()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;

                // Calcular el lunes de la semana actual
                int diasDesdeInicio = ((int)hoy.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var lunes = hoy.AddDays(-diasDesdeInicio);
                var sabado = lunes.AddDays(5);

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha.Date >= lunes && m.Fecha.Date <= sabado)
                    .ToListAsync();

                // Resumen por día
                var resumenPorDia = new List<object>();
                for (int i = 0; i < 6; i++)
                {
                    var dia = lunes.AddDays(i);
                    var movimientosDia = movimientos.Where(m => m.Fecha.Date == dia.Date).ToList();

                    var ingresos = movimientosDia.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                    var egresos = movimientosDia.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                    var balance = ingresos - egresos;

                    var ingresosEfectivo = movimientosDia.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                    var ingresosTransferencia = movimientosDia.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                    resumenPorDia.Add(new
                    {
                        fecha = dia,
                        diaNombre = dia.ToString("dddd", new System.Globalization.CultureInfo("es-ES")),
                        ingresos,
                        egresos,
                        balance,
                        ingresosEfectivo,
                        ingresosTransferencia,
                        cantidadMovimientos = movimientosDia.Count
                    });
                }

                // Totales de la semana
                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceTotal = totalIngresos - totalEgresos;

                var totalIngresosEfectivo = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var totalIngresosTransferencia = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                // Top categorías
                var topCategorias = movimientos
                    .Where(m => m.Tipo == "ingreso")
                    .GroupBy(m => m.Categoria)
                    .Select(g => new {
                        categoria = g.Key,
                        total = g.Sum(m => m.Monto),
                        cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.total)
                    .Take(5)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    periodo = new
                    {
                        desde = lunes,
                        hasta = sabado
                    },
                    resumenPorDia,
                    totales = new
                    {
                        ingresos = totalIngresos,
                        egresos = totalEgresos,
                        balance = balanceTotal,
                        ingresosEfectivo = totalIngresosEfectivo,
                        ingresosTransferencia = totalIngresosTransferencia,
                        cantidadMovimientos = movimientos.Count
                    },
                    topCategorias
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener resumen semanal",
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