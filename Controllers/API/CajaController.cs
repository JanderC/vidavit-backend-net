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
        /// Obtener la fecha del último cierre de caja
        /// </summary>
        private async Task<DateTime> ObtenerFechaUltimoCierreAsync()
        {
            var ultimoCierre = await _context.CierresCaja
                .OrderByDescending(c => c.FechaCierre)
                .FirstOrDefaultAsync();

            if (ultimoCierre == null)
            {
                return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
        }

        /// <summary>
        /// Obtener movimientos pendientes (no cerrados)
        /// </summary>
        private async Task<List<MovimientoCaja>> ObtenerMovimientosPendientesAsync()
        {
            var fechaUltimoCierre = await ObtenerFechaUltimoCierreAsync();

            var movimientos = await _context.MovimientosCaja
                .Where(m => m.Fecha > fechaUltimoCierre)
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            return movimientos;
        }

        /// <summary>
        /// Dashboard principal - estado actual de la caja
        /// Endpoint: GET /api/caja/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var movimientos = await ObtenerMovimientosPendientesAsync();
                var hoy = DateTime.UtcNow.Date;
                var movimientosHoy = movimientos.Where(m => m.Fecha.Date == hoy).ToList();

                var ingresosHoy = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresosHoy = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceHoy = ingresosHoy - egresosHoy;

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceTotal = totalIngresos - totalEgresos;

                var efectivo = movimientos
                    .Where(m => m.MetodoPago == "efectivo")
                    .GroupBy(m => m.Tipo)
                    .Select(g => new { tipo = g.Key, total = g.Sum(m => m.Monto) })
                    .ToList();

                var transferencia = movimientos
                    .Where(m => m.MetodoPago == "transferencia")
                    .GroupBy(m => m.Tipo)
                    .Select(g => new { tipo = g.Key, total = g.Sum(m => m.Monto) })
                    .ToList();

                var ingresosEfectivo = efectivo.FirstOrDefault(e => e.tipo == "ingreso")?.total ?? 0;
                var egresosEfectivo = efectivo.FirstOrDefault(e => e.tipo == "egreso")?.total ?? 0;
                var balanceEfectivo = ingresosEfectivo - egresosEfectivo;

                var ingresosTransferencia = transferencia.FirstOrDefault(t => t.tipo == "ingreso")?.total ?? 0;
                var egresosTransferencia = transferencia.FirstOrDefault(t => t.tipo == "egreso")?.total ?? 0;
                var balanceTransferencia = ingresosTransferencia - egresosTransferencia;

                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaCierre,
                        c.TipoCierre,
                        c.BalanceGeneral,
                        c.EfectivoFinal
                    })
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    cajaActual = new
                    {
                        totalIngresos,
                        totalEgresos,
                        balance = balanceTotal,
                        cantidadMovimientos = movimientos.Count,
                        efectivo = new
                        {
                            ingresos = ingresosEfectivo,
                            egresos = egresosEfectivo,
                            balance = balanceEfectivo
                        },
                        transferencia = new
                        {
                            ingresos = ingresosTransferencia,
                            egresos = egresosTransferencia,
                            balance = balanceTransferencia
                        }
                    },
                    hoy = new
                    {
                        ingresos = ingresosHoy,
                        egresos = egresosHoy,
                        balance = balanceHoy,
                        cantidadMovimientos = movimientosHoy.Count
                    },
                    ultimoCierre
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener dashboard",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener movimientos de HOY (pendientes)
        /// Endpoint: GET /api/caja/hoy
        /// </summary>
        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                var movimientos = await ObtenerMovimientosPendientesAsync();
                var hoy = DateTime.UtcNow.Date;
                var movimientosHoy = movimientos.Where(m => m.Fecha.Date == hoy).ToList();

                var ingresos = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresos = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balance = ingresos - egresos;

                var ingresosEfectivo = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);

                var ingresosTransferencia = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    ingresos,
                    egresos,
                    balance,
                    ingresosEfectivo,
                    egresosEfectivo,
                    ingresosTransferencia,
                    egresosTransferencia,
                    cantidadMovimientos = movimientosHoy.Count
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
        /// Obtener saldo del cierre anterior
        /// Endpoint: GET /api/caja/saldo-anterior
        /// </summary>
        [HttpGet("saldo-anterior")]
        public async Task<IActionResult> GetSaldoAnterior()
        {
            try
            {
                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .FirstOrDefaultAsync();

                if (ultimoCierre == null)
                {
                    return Ok(new
                    {
                        success = true,
                        saldoAnterior = 0,
                        fechaCierre = (DateTime?)null,
                        mensaje = "No hay cierres anteriores"
                    });
                }

                return Ok(new
                {
                    success = true,
                    saldoAnterior = ultimoCierre.EfectivoFinal,
                    fechaCierre = ultimoCierre.FechaCierre,
                    tipoCierre = ultimoCierre.TipoCierre
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
        /// Lista de movimientos con filtros
        /// Endpoint: GET /api/caja/movimientos
        /// </summary>
        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
            [FromQuery] string desde = null,
            [FromQuery] string hasta = null,
            [FromQuery] string categoria = null)
        {
            try
            {
                var movimientos = await ObtenerMovimientosPendientesAsync();

                if (!string.IsNullOrWhiteSpace(desde) && DateTime.TryParse(desde, out DateTime fechaDesde))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date >= fechaDesde.Date).ToList();
                }

                if (!string.IsNullOrWhiteSpace(hasta) && DateTime.TryParse(hasta, out DateTime fechaHasta))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date <= fechaHasta.Date).ToList();
                }

                if (!string.IsNullOrWhiteSpace(categoria))
                {
                    movimientos = movimientos.Where(m => m.Categoria == categoria).ToList();
                }

                var movimientosOrdenados = movimientos
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
                        m.ReferenciaId
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    movimientos = movimientosOrdenados
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
        /// Historial de cierres
        /// Endpoint: GET /api/caja/historial-cierres
        /// </summary>
        [HttpGet("historial-cierres")]
        public async Task<IActionResult> GetHistorialCierres([FromQuery] int limit = 20)
        {
            try
            {
                var cierres = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Take(limit)
                    .Select(c => new
                    {
                        c.Id,
                        fechaCierre = c.FechaCierre,
                        tipo = c.TipoCierre,
                        efectivoInicial = c.EfectivoInicial,
                        efectivoFinal = c.EfectivoFinal,
                        totalIngresos = c.TotalIngresos,
                        totalEgresos = c.TotalEgresos,
                        balanceGeneral = c.BalanceGeneral,
                        cantidadMovimientos = c.CantidadMovimientos,
                        observaciones = c.Observaciones
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    cierres
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
        /// Resumen semanal (lunes a sábado)
        /// Endpoint: GET /api/caja/resumen-semanal
        /// </summary>
        [HttpGet("resumen-semanal")]
        public async Task<IActionResult> GetResumenSemanal()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;

                int diasDesdeInicio = ((int)hoy.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var lunes = hoy.AddDays(-diasDesdeInicio);
                var sabado = lunes.AddDays(5);

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha.Date >= lunes && m.Fecha.Date <= sabado)
                    .ToListAsync();

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

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceTotal = totalIngresos - totalEgresos;

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
                        cantidadMovimientos = movimientos.Count
                    }
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
        /// Registrar ingreso
        /// Endpoint: POST /api/caja/ingreso
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
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar egreso
        /// Endpoint: POST /api/caja/egreso
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
        /// Cierre manual de caja
        /// Endpoint: POST /api/caja/cerrar
        /// </summary>
        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarCaja([FromBody] JsonElement data)
        {
            try
            {
                var observaciones = "";
                if (data.TryGetProperty("observaciones", out var obsEl))
                {
                    observaciones = obsEl.GetString();
                }

                var movimientos = await ObtenerMovimientosPendientesAsync();

                if (movimientos.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No hay movimientos pendientes para cerrar"
                    });
                }

                var ingresosEfectivo = movimientos
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivo = movimientos
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var ingresosTransferencia = movimientos
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var egresosTransferencia = movimientos
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                var efectivoInicial = 0;
                var efectivoFinal = ingresosEfectivo - egresosEfectivo;
                var balanceGeneral = totalIngresos - totalEgresos;

                var usuarioId = await ObtenerUsuarioSistemaAsync();

                var cierre = new CierreCaja
                {
                    Id = Guid.NewGuid(),
                    FechaCierre = DateTime.UtcNow,
                    TipoCierre = "manual",
                    EfectivoInicial = efectivoInicial,
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
                    message = "Cierre de caja realizado correctamente. La caja ahora está en cero.",
                    data = new
                    {
                        cierre.Id,
                        cierre.FechaCierre,
                        cierre.TipoCierre,
                        cierre.EfectivoFinal,
                        cierre.BalanceGeneral,
                        cierre.TotalIngresos,
                        cierre.TotalEgresos,
                        cierre.CantidadMovimientos
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al realizar cierre de caja",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Eliminar movimiento
        /// Endpoint: DELETE /api/caja/{id}
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
    }
}