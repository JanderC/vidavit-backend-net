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
                // Si no hay cierres previos, usar inicio del día actual
                return DateTime.UtcNow.Date;
            }

            return DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
        }

        /// <summary>
        /// Obtener movimientos del día actual (desde las 00:00 de hoy)
        /// </summary>
        private async Task<List<MovimientoCaja>> ObtenerMovimientosDiaActualAsync()
        {
            var hoy = DateTime.UtcNow.Date;

            var movimientos = await _context.MovimientosCaja
                .Where(m => m.Fecha >= hoy && m.Fecha < hoy.AddDays(1))
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
                var hoy = DateTime.UtcNow.Date;
                var movimientosHoy = await ObtenerMovimientosDiaActualAsync();

                // Calcular totales de HOY
                var ingresosHoy = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresosHoy = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceHoy = ingresosHoy - egresosHoy;

                // Desglose por método de pago de HOY
                var ingresosEfectivo = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivo = movimientosHoy
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var balanceEfectivo = ingresosEfectivo - egresosEfectivo;

                var ingresosTransferencia = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var egresosTransferencia = movimientosHoy
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var balanceTransferencia = ingresosTransferencia - egresosTransferencia;

                // Desglose por categoría de HOY
                var ingresoMembresias = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && (m.Categoria == "membresia" || m.Categoria == "renovacion"))
                    .Sum(m => m.Monto);

                var ingresoProductos = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.Categoria == "producto")
                    .Sum(m => m.Monto);

                var ingresoAbonos = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.Categoria == "abono_deuda")
                    .Sum(m => m.Monto);

                var ingresoOtros = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.Categoria != "membresia" && m.Categoria != "renovacion" && m.Categoria != "producto" && m.Categoria != "abono_deuda")
                    .Sum(m => m.Monto);

                // Obtener último cierre para información histórica
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
                    cajaHoy = new
                    {
                        totalIngresos = ingresosHoy,
                        totalEgresos = egresosHoy,
                        balance = balanceHoy,
                        cantidadMovimientos = movimientosHoy.Count,
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
                        },
                        categorias = new
                        {
                            membresias = ingresoMembresias,
                            productos = ingresoProductos,
                            abonos = ingresoAbonos,
                            otros = ingresoOtros
                        }
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
        /// Obtener movimientos de HOY
        /// Endpoint: GET /api/caja/hoy
        /// </summary>
        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                var movimientosHoy = await ObtenerMovimientosDiaActualAsync();

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
        /// Obtener movimientos con filtros
        /// Endpoint: GET /api/caja/movimientos
        /// </summary>
        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            [FromQuery] string? categoria = null,
            [FromQuery] string? tipo = null)
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var fechaDesde = desde ?? hoy;
                var fechaHasta = hasta ?? hoy.AddDays(1).AddTicks(-1);

                var query = _context.MovimientosCaja
                    .Where(m => m.Fecha >= fechaDesde && m.Fecha <= fechaHasta);

                if (!string.IsNullOrEmpty(categoria))
                {
                    query = query.Where(m => m.Categoria == categoria);
                }

                if (!string.IsNullOrEmpty(tipo))
                {
                    query = query.Where(m => m.Tipo == tipo);
                }

                var movimientos = await query
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        Tipo = m.Tipo,
                        Categoria = m.Categoria,
                        Monto = m.Monto,
                        Descripcion = m.Descripcion,
                        MetodoPago = m.MetodoPago,
                        Fecha = m.Fecha,
                        ReferenciaId = m.ReferenciaId
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    movimientos = movimientos,
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
        /// Resumen semanal
        /// Endpoint: GET /api/caja/semanal
        /// </summary>
        [HttpGet("semanal")]
        public async Task<IActionResult> GetResumenSemanal()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var hace7Dias = hoy.AddDays(-6);

                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= hace7Dias && m.Fecha <= hoy.AddDays(1).AddTicks(-1))
                    .ToListAsync();

                var resumenPorDia = movimientos
                    .GroupBy(m => m.Fecha.Date)
                    .Select(g => new
                    {
                        fecha = g.Key,
                        diaNombre = g.Key.ToString("dddd, dd/MM"),
                        ingresos = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                        egresos = g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        balance = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) - g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        ingresosEfectivo = g.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        ingresosTransferencia = g.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                        productosVendidos = g.Where(m => m.Tipo == "ingreso" && m.Categoria == "producto").Sum(m => m.Monto),
                        cantidadMovimientos = g.Count()
                    })
                    .OrderBy(r => r.fecha)
                    .ToList();

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceTotal = totalIngresos - totalEgresos;
                var totalProductos = movimientos.Where(m => m.Tipo == "ingreso" && m.Categoria == "producto").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    periodo = new
                    {
                        desde = hace7Dias,
                        hasta = hoy
                    },
                    resumenPorDia,
                    totales = new
                    {
                        ingresos = totalIngresos,
                        egresos = totalEgresos,
                        balance = balanceTotal,
                        productosVendidos = totalProductos,
                        cantidadMovimientos = movimientos.Count,
                        dias = resumenPorDia.Count
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
        /// Historial de cierres
        /// Endpoint: GET /api/caja/cierres
        /// </summary>
        [HttpGet("cierres")]
        public async Task<IActionResult> GetHistorialCierres([FromQuery] int limite = 10)
        {
            try
            {
                var cierres = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Take(limite)
                    .Select(c => new
                    {
                        c.Id,
                        fechaCierre = c.FechaCierre,
                        tipo = c.TipoCierre,
                        efectivoInicial = c.EfectivoInicial,
                        ingresosEfectivo = c.IngresosEfectivo,
                        egresosEfectivo = c.EgresosEfectivo,
                        efectivoFinal = c.EfectivoFinal,
                        ingresosTransferencia = c.IngresosTransferencia,
                        egresosTransferencia = c.EgresosTransferencia,
                        totalIngresos = c.TotalIngresos,
                        totalEgresos = c.TotalEgresos,
                        balanceGeneral = c.BalanceGeneral,
                        cantidadMovimientos = c.CantidadMovimientos,
                        observaciones = c.Observaciones ?? ""
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    cierres = cierres,
                    total = cierres.Count
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

                // Obtener movimientos del día actual
                var movimientosHoy = await ObtenerMovimientosDiaActualAsync();

                if (movimientosHoy.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No hay movimientos del día para cerrar"
                    });
                }

                var ingresosEfectivo = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivo = movimientosHoy
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var ingresosTransferencia = movimientosHoy
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var egresosTransferencia = movimientosHoy
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var totalIngresos = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                var efectivoInicial = 0; // Siempre empezamos en 0 cada día
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
                    CantidadMovimientos = movimientosHoy.Count,
                    Observaciones = observaciones,
                    UsuarioId = usuarioId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CierresCaja.Add(cierre);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cierre de caja realizado correctamente",
                    data = new
                    {
                        cierre.Id,
                        cierre.FechaCierre,
                        cierre.TipoCierre,
                        efectivoFinal = cierre.EfectivoFinal,
                        transferenciaTotal = ingresosTransferencia - egresosTransferencia,
                        balanceGeneral = cierre.BalanceGeneral,
                        totalIngresos = cierre.TotalIngresos,
                        totalEgresos = cierre.TotalEgresos,
                        cantidadMovimientos = cierre.CantidadMovimientos,
                        desglose = new
                        {
                            efectivo = new
                            {
                                ingresos = ingresosEfectivo,
                                egresos = egresosEfectivo,
                                balance = efectivoFinal
                            },
                            transferencia = new
                            {
                                ingresos = ingresosTransferencia,
                                egresos = egresosTransferencia,
                                balance = ingresosTransferencia - egresosTransferencia
                            }
                        }
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