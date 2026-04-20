using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class CajaController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CajaController> _logger;
        private readonly CajaFuerteController _cajaFuerteController;

        public CajaController(AppDbContext context, ILogger<CajaController> logger, CajaFuerteController cajaFuerteController)
        {
            _context = context;
            _logger = logger;
            _cajaFuerteController = cajaFuerteController;
        }

        // =============================================
        // MÉTODOS PRIVADOS AUXILIARES
        // =============================================

        private async Task<Guid> ObtenerUsuarioSistemaAsync()
        {
            var usuario = await _context.Usuarios
                .Where(u => u.Activo)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (usuario == Guid.Empty)
                throw new Exception("No hay usuarios activos en el sistema");

            return usuario;
        }

        private async Task<List<MovimientoCaja>> ObtenerMovimientosPendientesPorFechaAsync(DateTime fecha)
        {
            var fechaBuscar = fecha.Date;
            var movimientos = await _context.MovimientosCaja
                .Where(m => !m.Cerrado)
                .ToListAsync();

            return movimientos
                .Where(m => m.Fecha.Date == fechaBuscar)
                .OrderBy(m => m.Fecha)
                .ToList();
        }

        private async Task<List<MovimientoCaja>> ObtenerTodosMovimientosPendientesAsync()
        {
            return await _context.MovimientosCaja
                .Where(m => !m.Cerrado)
                .OrderBy(m => m.Fecha)
                .ToListAsync();
        }

        // =============================================
        // DASHBOARD
        // =============================================

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var hoy = DateTime.Now.Date;
                var movimientosHoy = await ObtenerMovimientosPendientesPorFechaAsync(hoy);

                var ingresosHoy = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresosHoy = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceHoy = ingresosHoy - egresosHoy;

                var ingresosEfectivo = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var balanceEfectivo = ingresosEfectivo - egresosEfectivo;

                var ingresosTransferencia = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var balanceTransferencia = ingresosTransferencia - egresosTransferencia;

                var ingresoMembresias = movimientosHoy.Where(m => m.Tipo == "ingreso" && (m.Categoria == "membresia" || m.Categoria == "renovacion")).Sum(m => m.Monto);
                var ingresoProductos = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.Categoria == "producto").Sum(m => m.Monto);
                var ingresoAbonos = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.Categoria == "abono_deuda").Sum(m => m.Monto);
                var ingresoOtros = movimientosHoy.Where(m => m.Tipo == "ingreso" &&
                    m.Categoria != "membresia" && m.Categoria != "renovacion" &&
                    m.Categoria != "producto" && m.Categoria != "abono_deuda").Sum(m => m.Monto);

                var ultimoCierre = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Select(c => new { c.Id, c.FechaCierre, c.TipoCierre, c.BalanceGeneral, c.EfectivoFinal })
                    .FirstOrDefaultAsync();

                var todosPendientes = await _context.MovimientosCaja.Where(m => !m.Cerrado).ToListAsync();
                var movimientosPendientesAnteriores = todosPendientes.Count(m => m.Fecha.Date < hoy);

                return Ok(new
                {
                    success = true,
                    cajaHoy = new
                    {
                        totalIngresos = ingresosHoy,
                        totalEgresos = egresosHoy,
                        balance = balanceHoy,
                        cantidadMovimientos = movimientosHoy.Count,
                        efectivo = new { ingresos = ingresosEfectivo, egresos = egresosEfectivo, balance = balanceEfectivo },
                        transferencia = new { ingresos = ingresosTransferencia, egresos = egresosTransferencia, balance = balanceTransferencia },
                        categorias = new { membresias = ingresoMembresias, productos = ingresoProductos, abonos = ingresoAbonos, otros = ingresoOtros }
                    },
                    ultimoCierre,
                    alertas = new { movimientosPendientesAnteriores, tieneMovimientosPendientesAnteriores = movimientosPendientesAnteriores > 0 }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener dashboard");
                return StatusCode(500, new { success = false, message = "Error al obtener dashboard", error = ex.Message });
            }
        }

        // =============================================
        // RESUMEN SEMANAL
        // =============================================

        [HttpGet("semanal")]
        public async Task<IActionResult> GetResumenSemanal()
        {
            try
            {
                var hoy = DateTime.Now.Date;
                var hace7Dias = hoy.AddDays(-6);

                var movimientos = await _context.MovimientosCaja.ToListAsync();
                var movimientosSemana = movimientos
                    .Where(m => { var f = m.Fecha.Date; return f >= hace7Dias && f <= hoy; })
                    .ToList();

                var resumenPorDia = new List<object>();
                for (int i = 0; i < 7; i++)
                {
                    var fecha = hace7Dias.AddDays(i);
                    var movimientosDia = movimientosSemana.Where(m => m.Fecha.Date == fecha).ToList();
                    var ingresos = movimientosDia.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                    var egresos = movimientosDia.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                    var cultura = new System.Globalization.CultureInfo("es-ES");
                    var diaNombre = fecha.ToString("dddd", cultura);
                    diaNombre = char.ToUpper(diaNombre[0]) + diaNombre.Substring(1);

                    resumenPorDia.Add(new
                    {
                        fecha = fecha.ToString("yyyy-MM-dd"),
                        diaNombre,
                        ingresos,
                        egresos,
                        balance = ingresos - egresos,
                        ingresosEfectivo = movimientosDia.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        ingresosTransferencia = movimientosDia.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                        productosVendidos = movimientosDia.Where(m => m.Tipo == "ingreso" && m.Categoria == "producto").Sum(m => m.Monto),
                        cantidadMovimientos = movimientosDia.Count
                    });
                }

                var totales = new
                {
                    ingresos = movimientosSemana.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                    egresos = movimientosSemana.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                    balance = movimientosSemana.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) - movimientosSemana.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                    productosVendidos = movimientosSemana.Where(m => m.Tipo == "ingreso" && m.Categoria == "producto").Sum(m => m.Monto),
                    cantidadMovimientos = movimientosSemana.Count
                };

                return Ok(new
                {
                    success = true,
                    periodo = new { desde = hace7Dias.ToString("yyyy-MM-dd"), hasta = hoy.ToString("yyyy-MM-dd") },
                    resumenPorDia,
                    totales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen semanal");
                return Ok(new { success = false, message = "Error al obtener resumen semanal", error = ex.Message });
            }
        }

        // =============================================
        // MOVIMIENTOS HOY
        // =============================================

        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                var hoy = DateTime.Now.Date;
                var movimientosHoy = await ObtenerMovimientosPendientesPorFechaAsync(hoy);

                var ingresos = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresos = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var ingresosEfectivo = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientosHoy.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosHoy.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    fecha = hoy,
                    resumen = new
                    {
                        totalIngresos = ingresos,
                        totalEgresos = egresos,
                        balance = ingresos - egresos,
                        efectivo = new { ingresos = ingresosEfectivo, egresos = egresosEfectivo, balance = ingresosEfectivo - egresosEfectivo },
                        transferencia = new { ingresos = ingresosTransferencia, egresos = egresosTransferencia, balance = ingresosTransferencia - egresosTransferencia }
                    },
                    movimientos = movimientosHoy.Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        fecha = m.Fecha.ToString("yyyy-MM-ddTHH:mm:ss"),
                        m.Cerrado,
                        m.MovimientoCajaFuerteId // ✅ Indica si viene de una transferencia desde CF
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos del día");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos del día", error = ex.Message });
            }
        }

        // =============================================
        // MOVIMIENTOS PENDIENTES POR FECHA
        // =============================================

        [HttpGet("pendientes")]
        public async Task<IActionResult> GetMovimientosPendientes()
        {
            try
            {
                var movimientosPendientes = await ObtenerTodosMovimientosPendientesAsync();
                var movimientosPorFecha = movimientosPendientes
                    .GroupBy(m => m.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        fecha = g.Key,
                        cantidad = g.Count(),
                        totalIngresos = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                        totalEgresos = g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        balance = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) - g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto)
                    }).ToList();

                return Ok(new { success = true, cantidadTotal = movimientosPendientes.Count, diasConMovimientos = movimientosPorFecha.Count, movimientosPorFecha });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos pendientes");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos pendientes", error = ex.Message });
            }
        }

        // =============================================
        // MOVIMIENTOS CON FILTROS
        // =============================================

        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
            [FromQuery] string? desde = null,
            [FromQuery] string? hasta = null,
            [FromQuery] bool soloPendientes = false)
        {
            try
            {
                var query = _context.MovimientosCaja.AsQueryable();
                if (soloPendientes) query = query.Where(m => !m.Cerrado);

                var todosMovimientos = await query.OrderByDescending(m => m.Fecha).ToListAsync();
                var movimientos = todosMovimientos;

                if (!string.IsNullOrEmpty(desde))
                {
                    var fechaDesde = DateTime.Parse(desde).Date;
                    movimientos = movimientos.Where(m => m.Fecha.Date >= fechaDesde).ToList();
                }
                if (!string.IsNullOrEmpty(hasta))
                {
                    var fechaHasta = DateTime.Parse(hasta).Date;
                    movimientos = movimientos.Where(m => m.Fecha.Date <= fechaHasta).ToList();
                }

                var resultado = movimientos.Select(m => new
                {
                    m.Id,
                    m.Tipo,
                    m.Categoria,
                    m.Monto,
                    m.Descripcion,
                    m.MetodoPago,
                    fecha = m.Fecha.ToString("yyyy-MM-ddTHH:mm:ss"),
                    m.Cerrado,
                    m.CierreCajaId,
                    m.MovimientoCajaFuerteId
                }).ToList();

                return Ok(new { success = true, filtros = new { fechaInicio = desde, fechaFin = hasta, soloPendientes }, cantidad = resultado.Count, data = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos");
                return Ok(new { success = false, message = "Error al obtener movimientos", error = ex.Message });
            }
        }

        // =============================================
        // HISTORIAL DE CIERRES
        // =============================================

        [HttpGet("cierres")]
        public async Task<IActionResult> GetCierres([FromQuery] int limite = 30)
        {
            try
            {
                var cierres = await _context.CierresCaja
                    .OrderByDescending(c => c.FechaCierre)
                    .Take(limite)
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaCierre,
                        c.TipoCierre,
                        c.EfectivoInicial,
                        c.IngresosEfectivo,
                        c.EgresosEfectivo,
                        c.EfectivoFinal,
                        c.IngresosTransferencia,
                        c.EgresosTransferencia,
                        c.TotalIngresos,
                        c.TotalEgresos,
                        c.BalanceGeneral,
                        c.CantidadMovimientos,
                        c.Observaciones,
                        c.UsuarioId
                    }).ToListAsync();

                return Ok(new { success = true, cantidad = cierres.Count, data = cierres });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cierres");
                return StatusCode(500, new { success = false, message = "Error al obtener cierres", error = ex.Message });
            }
        }

        // =============================================
        // DETALLE DE UN CIERRE (con movimientos del día)
        // =============================================

        [HttpGet("cierres/{id}")]
        public async Task<IActionResult> GetDetalleCierre(Guid id)
        {
            try
            {
                var cierre = await _context.CierresCaja.FindAsync(id);
                if (cierre == null)
                    return NotFound(new { success = false, message = "Cierre no encontrado" });

                var movimientosCierre = await _context.MovimientosCaja
                    .Where(m => m.CierreCajaId == id)
                    .OrderBy(m => m.Fecha)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    cierre = new
                    {
                        cierre.Id,
                        cierre.FechaCierre,
                        cierre.TipoCierre,
                        cierre.EfectivoInicial,
                        cierre.IngresosEfectivo,
                        cierre.EgresosEfectivo,
                        cierre.EfectivoFinal,
                        cierre.IngresosTransferencia,
                        cierre.EgresosTransferencia,
                        cierre.TotalIngresos,
                        cierre.TotalEgresos,
                        cierre.BalanceGeneral,
                        cierre.CantidadMovimientos,
                        cierre.Observaciones
                    },
                    movimientos = movimientosCierre.Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        fecha = m.Fecha.ToString("yyyy-MM-ddTHH:mm:ss"),
                        m.MovimientoCajaFuerteId
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de cierre");
                return StatusCode(500, new { success = false, message = "Error al obtener detalle", error = ex.Message });
            }
        }

        // =============================================
        // REGISTRAR MOVIMIENTO MANUAL
        // =============================================

        [HttpPost("registrar")]
        public async Task<IActionResult> RegistrarMovimiento([FromBody] RegistrarMovimientoRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                var usuarioId = await ObtenerUsuarioSistemaAsync();

                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = request.Tipo,
                    Categoria = request.Categoria,
                    MetodoPago = request.MetodoPago,
                    Monto = request.Monto,
                    Descripcion = request.Descripcion,
                    ReferenciaId = request.ReferenciaId,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CierreCajaId = null,
                    MovimientoCajaFuerteId = null,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Movimiento registrado: {request.Tipo} ${request.Monto} ({request.Categoria})");

                return Ok(new
                {
                    success = true,
                    message = "Movimiento registrado correctamente",
                    movimiento = new { movimiento.Id, movimiento.Tipo, movimiento.Monto, movimiento.Descripcion, movimiento.Fecha }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar movimiento");
                return StatusCode(500, new { success = false, message = "Error al registrar movimiento", error = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS ALIAS: /ingreso y /egreso
        // El frontend llama a estas rutas por separado.
        // Reutilizan el mismo RegistrarMovimiento seteando Tipo.
        // =============================================

        [HttpPost("ingreso")]
        public async Task<IActionResult> RegistrarIngreso([FromBody] IngresoEgresoRequest request)
        {
            return await RegistrarMovimiento(new RegistrarMovimientoRequest
            {
                Tipo = "ingreso",
                Categoria = request.Categoria,
                Monto = request.Monto,
                Descripcion = request.Descripcion,
                MetodoPago = request.MetodoPago,
                ReferenciaId = request.ReferenciaId
            });
        }

        [HttpPost("egreso")]
        public async Task<IActionResult> RegistrarEgreso([FromBody] IngresoEgresoRequest request)
        {
            return await RegistrarMovimiento(new RegistrarMovimientoRequest
            {
                Tipo = "egreso",
                Categoria = request.Categoria,
                Monto = request.Monto,
                Descripcion = request.Descripcion,
                MetodoPago = request.MetodoPago,
                ReferenciaId = request.ReferenciaId
            });
        }

        // =============================================
        // CIERRE DE CAJA
        // =============================================

        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarCaja([FromBody] CerrarCajaRequest request)
        {
            try
            {
                DateTime fechaCierre;
                if (string.IsNullOrEmpty(request.Fecha) || !DateTime.TryParse(request.Fecha, out fechaCierre))
                    fechaCierre = DateTime.Now.Date;
                else
                    fechaCierre = fechaCierre.Date;

                var movimientosPendientes = await ObtenerMovimientosPendientesPorFechaAsync(fechaCierre);

                if (!movimientosPendientes.Any())
                    return BadRequest(new { success = false, message = $"No hay movimientos pendientes para cerrar el día {fechaCierre:dd/MM/yyyy}" });

                var usuarioId = await ObtenerUsuarioSistemaAsync();

                var ingresosEfectivo = movimientosPendientes.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosPendientes.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientosPendientes.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosPendientes.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                var balanceEfectivo = ingresosEfectivo - egresosEfectivo;
                var efectivoADejar = request.EfectivoADejar ?? 0;

                if (efectivoADejar > balanceEfectivo)
                    return BadRequest(new { success = false, message = $"El efectivo a dejar (${efectivoADejar}) no puede ser mayor al balance de efectivo (${balanceEfectivo})" });

                var efectivoACajaFuerte = balanceEfectivo - efectivoADejar;
                var totalIngresos = ingresosEfectivo + ingresosTransferencia;
                var totalEgresos = egresosEfectivo + egresosTransferencia;

                var cierre = new CierreCaja
                {
                    Id = Guid.NewGuid(),
                    FechaCierre = fechaCierre,
                    TipoCierre = "diario",
                    EfectivoInicial = 0,
                    IngresosEfectivo = ingresosEfectivo,
                    EgresosEfectivo = egresosEfectivo,
                    EfectivoFinal = efectivoACajaFuerte,
                    IngresosTransferencia = ingresosTransferencia,
                    EgresosTransferencia = egresosTransferencia,
                    TotalIngresos = totalIngresos,
                    TotalEgresos = totalEgresos,
                    BalanceGeneral = totalIngresos - totalEgresos,
                    CantidadMovimientos = movimientosPendientes.Count,
                    Observaciones = $"{request.Observaciones} - Cierre del día {fechaCierre:dd/MM/yyyy}. Efectivo a dejar: ${efectivoADejar}, a Caja Fuerte: ${efectivoACajaFuerte}".Trim(),
                    UsuarioId = usuarioId,
                    CreatedAt = DateTime.Now
                };

                _context.CierresCaja.Add(cierre);
                await _context.SaveChangesAsync();

                foreach (var movimiento in movimientosPendientes)
                {
                    movimiento.Cerrado = true;
                    movimiento.CierreCajaId = cierre.Id;
                }
                await _context.SaveChangesAsync();

                // ✅ Enviar a Caja Fuerte (los cierres siempre van a CF con detalle completo)
                var montoTransferenciaNetoACF = ingresosTransferencia - egresosTransferencia;
                if (efectivoACajaFuerte > 0 || montoTransferenciaNetoACF > 0)
                {
                    try
                    {
                        var enviado = await _cajaFuerteController.RecibirDesdeCajaInternoAsync(
                            montoEfectivo: efectivoACajaFuerte,
                            montoTransferencia: montoTransferenciaNetoACF,
                            fechaCierre: fechaCierre,
                            cierreCajaId: cierre.Id
                        );

                        if (enviado)
                            _logger.LogInformation($"✅ Dinero enviado a Caja Fuerte: Efectivo=${efectivoACajaFuerte}, Transferencias={montoTransferenciaNetoACF}");
                        else
                            _logger.LogWarning($"⚠️ El cierre {cierre.Id} ya estaba procesado en Caja Fuerte");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar dinero a Caja Fuerte desde cierre");
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Cierre de caja realizado correctamente para el día {fechaCierre:dd/MM/yyyy}",
                    data = new
                    {
                        cierre.Id,
                        cierre.FechaCierre,
                        fechaDiaCerrado = fechaCierre,
                        cierre.TipoCierre,
                        efectivoFinal = cierre.EfectivoFinal,
                        efectivoADejar,
                        efectivoACajaFuerte,
                        transferenciaTotal = ingresosTransferencia - egresosTransferencia,
                        balanceGeneral = cierre.BalanceGeneral,
                        totalIngresos = cierre.TotalIngresos,
                        totalEgresos = cierre.TotalEgresos,
                        cantidadMovimientos = cierre.CantidadMovimientos,
                        enviadoACajaFuerte = efectivoACajaFuerte > 0 || montoTransferenciaNetoACF > 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al realizar cierre de caja");
                return StatusCode(500, new { success = false, message = "Error al realizar cierre de caja", error = ex.Message });
            }
        }

        // =============================================
        // ✅ ELIMINAR MOVIMIENTO — CON AUDITORÍA Y ESPEJO EN CF
        // =============================================

        /// <summary>
        /// DELETE /api/caja/{id}
        /// - Movimiento NO cerrado y sin vínculo CF: elimina directo.
        /// - Movimiento con MovimientoCajaFuerteId: elimina también el espejo en CF y revierte balance.
        /// - Movimiento cerrado sin vínculo CF: rechazado (no editar cierres ya cerrados).
        /// Siempre registra en movimientos_eliminados para auditoría.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarMovimiento(Guid id, [FromBody] EliminarMovimientoRequest? request = null)
        {
            try
            {
                var movimiento = await _context.MovimientosCaja.FindAsync(id);
                if (movimiento == null)
                    return NotFound(new { success = false, message = "Movimiento no encontrado" });

                // Si está cerrado y no tiene vínculo con CF, no se puede tocar
                if (movimiento.Cerrado && movimiento.MovimientoCajaFuerteId == null)
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se puede eliminar un movimiento que ya fue procesado en un cierre de caja. Solo se pueden eliminar movimientos pendientes o transferencias desde Caja Fuerte."
                    });

                var usuarioId = await ObtenerUsuarioSistemaAsync();
                var motivo = request?.Motivo ?? "Sin motivo especificado";

                // Registrar en auditoría (Caja Diaria)
                _context.MovimientosEliminados.Add(new MovimientoEliminado
                {
                    Id = Guid.NewGuid(),
                    MovimientoOriginalId = movimiento.Id,
                    ModuloOrigen = "caja_diaria",
                    Tipo = movimiento.Tipo,
                    Origen = movimiento.Categoria ?? "manual",
                    MetodoPago = movimiento.MetodoPago ?? "efectivo",
                    Monto = movimiento.Monto,
                    Descripcion = movimiento.Descripcion ?? "",
                    Categoria = movimiento.Categoria ?? "",
                    FechaOriginal = movimiento.Fecha,
                    FechaEliminacion = DateTime.Now,
                    UsuarioEliminacion = usuarioId,
                    MotivoEliminacion = motivo,
                    CreatedAt = DateTime.Now
                });

                // ✅ Si el movimiento vino de Caja Fuerte → eliminar el espejo en CF y revertir balance
                if (movimiento.MovimientoCajaFuerteId.HasValue)
                {
                    var movCF = await _context.MovimientosCajaFuerte.FindAsync(movimiento.MovimientoCajaFuerteId.Value);
                    if (movCF != null)
                    {
                        var cajaFuerte = await _cajaFuerteController.ObtenerOCrearCajaFuerteAsync();

                        // En CF el movimiento fue un "egreso" (salió dinero hacia Caja Diaria) → revertir sumando
                        if (movCF.Tipo == "egreso")
                        {
                            if (movCF.MetodoPago == "efectivo") cajaFuerte.BalanceEfectivo += movCF.Monto;
                            else cajaFuerte.BalanceTransferencias += movCF.Monto;
                        }
                        else
                        {
                            if (movCF.MetodoPago == "efectivo") cajaFuerte.BalanceEfectivo -= movCF.Monto;
                            else cajaFuerte.BalanceTransferencias -= movCF.Monto;
                        }

                        cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                        cajaFuerte.UltimaActualizacion = DateTime.Now;
                        cajaFuerte.UpdatedAt = DateTime.Now;

                        // Auditoría del espejo eliminado en CF
                        _context.MovimientosEliminados.Add(new MovimientoEliminado
                        {
                            Id = Guid.NewGuid(),
                            MovimientoOriginalId = movCF.Id,
                            ModuloOrigen = "caja_fuerte",
                            Tipo = movCF.Tipo,
                            Origen = movCF.Origen,
                            MetodoPago = movCF.MetodoPago,
                            Monto = movCF.Monto,
                            Descripcion = movCF.Descripcion ?? "",
                            Categoria = movCF.Categoria ?? "",
                            FechaOriginal = movCF.Fecha,
                            FechaEliminacion = DateTime.Now,
                            UsuarioEliminacion = usuarioId,
                            MotivoEliminacion = $"Eliminado en cascada desde Caja Diaria. Motivo: {motivo}",
                            CreatedAt = DateTime.Now
                        });

                        _context.MovimientosCajaFuerte.Remove(movCF);
                        _logger.LogInformation($"🔄 Espejo CF eliminado y balance revertido: ${movCF.Monto}. Nuevo CF total: ${cajaFuerte.BalanceTotal}");
                    }
                }

                _context.MovimientosCaja.Remove(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"🗑️ Movimiento Caja Diaria eliminado: ${movimiento.Monto} ({movimiento.Tipo}) — {motivo}");

                return Ok(new { success = true, message = "Movimiento eliminado y registrado en auditoría correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar movimiento");
                return StatusCode(500, new { success = false, message = "Error al eliminar movimiento", error = ex.Message });
            }
        }

        // =============================================
        // ✅ HISTORIAL DE ELIMINADOS — AUDITORÍA CAJA DIARIA
        // =============================================

        /// <summary>
        /// GET /api/caja/eliminados?desde=&hasta=
        /// Historial de movimientos eliminados de Caja Diaria para auditoría.
        /// </summary>
        [HttpGet("eliminados")]
        public async Task<IActionResult> GetMovimientosEliminados(
            [FromQuery] string? desde = null,
            [FromQuery] string? hasta = null)
        {
            try
            {
                var todos = await _context.MovimientosEliminados
                    .Where(m => m.ModuloOrigen == "caja_diaria")
                    .OrderByDescending(m => m.FechaEliminacion)
                    .ToListAsync();

                if (!string.IsNullOrEmpty(desde))
                {
                    var fechaDesde = DateTime.Parse(desde).Date;
                    todos = todos.Where(m => m.FechaEliminacion.Date >= fechaDesde).ToList();
                }
                if (!string.IsNullOrEmpty(hasta))
                {
                    var fechaHasta = DateTime.Parse(hasta).Date;
                    todos = todos.Where(m => m.FechaEliminacion.Date <= fechaHasta).ToList();
                }

                return Ok(new
                {
                    success = true,
                    cantidad = todos.Count,
                    totalMonto = todos.Sum(m => m.Monto),
                    data = todos.Select(m => new
                    {
                        m.Id,
                        m.MovimientoOriginalId,
                        m.ModuloOrigen,
                        m.Tipo,
                        m.Origen,
                        m.MetodoPago,
                        m.Monto,
                        m.Descripcion,
                        m.Categoria,
                        fechaOriginal = m.FechaOriginal.ToString("yyyy-MM-ddTHH:mm:ss"),
                        fechaEliminacion = m.FechaEliminacion.ToString("yyyy-MM-ddTHH:mm:ss"),
                        m.MotivoEliminacion
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos eliminados");
                return StatusCode(500, new { success = false, message = "Error al obtener eliminados", error = ex.Message });
            }
        }

        // =============================================
        // RECIBIR DESDE CAJA FUERTE (llamado desde CajaFuerteController)
        // =============================================

        /// <summary>
        /// POST /api/caja/recibir-desde-cajafuerte
        /// Crea el movimiento ingreso en Caja Diaria con referencia al movimiento CF que lo originó.
        /// Llamado internamente desde CajaFuerteController.TransferirACaja.
        /// </summary>
        [HttpPost("recibir-desde-cajafuerte")]
        public async Task<IActionResult> RecibirDesdeCajaFuerte([FromBody] RecibirDesdeCajaFuerteRequest request)
        {
            try
            {
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = "transferencia_cajafuerte",
                    MetodoPago = "efectivo",
                    Monto = request.Monto,
                    Descripcion = string.IsNullOrEmpty(request.Descripcion)
                        ? "Transferencia desde Caja Fuerte"
                        : $"Transferencia desde Caja Fuerte: {request.Descripcion}",
                    ReferenciaId = null,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CierreCajaId = null,
                    // ✅ CLAVE: vincula este movimiento con el de CF para poder eliminarlo en cascada
                    MovimientoCajaFuerteId = request.MovimientoCajaFuerteId,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"💰 Transferencia CF→Caja Diaria: ${request.Monto} (CF_movId: {request.MovimientoCajaFuerteId})");

                return Ok(new
                {
                    success = true,
                    message = $"${request.Monto:N2} recibidos desde Caja Fuerte en Caja Diaria",
                    movimiento = new { movimiento.Id, movimiento.Monto, movimiento.Descripcion, movimiento.Fecha }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recibir dinero desde Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al recibir dinero desde Caja Fuerte" });
            }
        }

        // =============================================
        // REQUEST MODELS
        // =============================================

        public class CerrarCajaRequest
        {
            public string Fecha { get; set; }
            public string Observaciones { get; set; }
            public decimal? EfectivoADejar { get; set; }
        }

        public class RegistrarMovimientoRequest
        {
            public string Tipo { get; set; }
            public string? Categoria { get; set; }
            public decimal Monto { get; set; }
            public string Descripcion { get; set; }
            public string MetodoPago { get; set; }
            public Guid? ReferenciaId { get; set; }
        }


        // Igual que RegistrarMovimientoRequest pero sin Tipo (lo pone el endpoint alias)
        public class IngresoEgresoRequest
        {
            public string? Categoria { get; set; }
            public decimal Monto { get; set; }
            public string Descripcion { get; set; }
            public string MetodoPago { get; set; }
            public Guid? ReferenciaId { get; set; }
        }
        public class RecibirDesdeCajaFuerteRequest
        {
            public decimal Monto { get; set; }
            public string? Descripcion { get; set; }
            // ✅ ID del movimiento en CF que originó esta transferencia (para vinculación bidireccional)
            public Guid? MovimientoCajaFuerteId { get; set; }
        }

        public class EliminarMovimientoRequest
        {
            public string? Motivo { get; set; }
        }
    }
}