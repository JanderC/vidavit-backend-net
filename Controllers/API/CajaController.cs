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
        private readonly ILogger<CajaController> _logger;
        // ✅ Inyectar CajaFuerteController para llamar directamente sin HTTP interno
        private readonly CajaFuerteController _cajaFuerteController;

        public CajaController(AppDbContext context, ILogger<CajaController> logger, CajaFuerteController cajaFuerteController)
        {
            _context = context;
            _logger = logger;
            _cajaFuerteController = cajaFuerteController;
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
        /// Obtener movimientos NO CERRADOS de una fecha específica
        /// </summary>
        private async Task<List<MovimientoCaja>> ObtenerMovimientosPendientesPorFechaAsync(DateTime fecha)
        {
            // Usar fecha local sin conversiones a UTC
            var fechaBuscar = fecha.Date;

            _logger.LogInformation($"🔍 Buscando movimientos pendientes para {fechaBuscar:yyyy-MM-dd}");

            // Obtener todos los movimientos no cerrados
            var movimientos = await _context.MovimientosCaja
                .Where(m => !m.Cerrado)
                .ToListAsync();

            // Filtrar en memoria por la fecha LOCAL (convertir UTC a local antes de comparar)
            var movimientosFiltrados = movimientos
                .Where(m => m.Fecha.ToLocalTime().Date == fechaBuscar)
                .OrderBy(m => m.Fecha)
                .ToList();

            _logger.LogInformation($"   ✅ Encontrados: {movimientosFiltrados.Count} movimientos");

            if (movimientosFiltrados.Count > 0)
            {
                _logger.LogInformation($"   📅 Primer mov: {movimientosFiltrados[0].Fecha:yyyy-MM-dd HH:mm:ss}");
                _logger.LogInformation($"   📅 Último mov: {movimientosFiltrados[^1].Fecha:yyyy-MM-dd HH:mm:ss}");
            }

            return movimientosFiltrados;
        }

        /// <summary>
        /// Obtener TODOS los movimientos NO CERRADOS (de cualquier fecha)
        /// </summary>
        private async Task<List<MovimientoCaja>> ObtenerTodosMovimientosPendientesAsync()
        {
            var movimientos = await _context.MovimientosCaja
                .Where(m => !m.Cerrado)
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            return movimientos;
        }

        /// <summary>
        /// Dashboard principal - estado actual de la caja SOLO DEL DÍA ACTUAL
        /// Endpoint: GET /api/caja/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                // CORRECCIÓN: Usar DateTime.Now.Date sin conversiones a UTC
                var hoy = DateTime.Now.Date;

                // SOLO movimientos NO cerrados del día de hoy
                var movimientosHoy = await ObtenerMovimientosPendientesPorFechaAsync(hoy);

                // Calcular totales
                var ingresosHoy = movimientosHoy.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egresosHoy = movimientosHoy.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                var balanceHoy = ingresosHoy - egresosHoy;

                // Desglose por método de pago
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

                // Desglose por categoría
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
                    .Where(m => m.Tipo == "ingreso" &&
                        m.Categoria != "membresia" &&
                        m.Categoria != "renovacion" &&
                        m.Categoria != "producto" &&
                        m.Categoria != "abono_deuda")
                    .Sum(m => m.Monto);

                // Obtener último cierre
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

                // Verificar si hay movimientos pendientes de días anteriores
                var todosPendientes = await _context.MovimientosCaja
                    .Where(m => !m.Cerrado)
                    .ToListAsync();

                var movimientosPendientesAnteriores = todosPendientes
                    .Count(m => m.Fecha.ToLocalTime().Date < hoy);

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
                    ultimoCierre = ultimoCierre,
                    alertas = new
                    {
                        movimientosPendientesAnteriores,
                        tieneMovimientosPendientesAnteriores = movimientosPendientesAnteriores > 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener dashboard");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener dashboard",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener resumen de los últimos 7 días
        /// Endpoint: GET /api/caja/semanal
        /// </summary>
        [HttpGet("semanal")]
        public async Task<IActionResult> GetResumenSemanal()
        {
            try
            {
                // CORRECCIÓN: Usar DateTime.Now.Date sin conversiones a UTC
                var hoy = DateTime.Now.Date;
                var hace7Dias = hoy.AddDays(-6); // Los últimos 7 días (incluyendo hoy)

                // Obtener todos los movimientos (sin filtro de fecha en la query)
                var movimientos = await _context.MovimientosCaja
                    .ToListAsync();

                // Filtrar en memoria por fecha local
                var movimientosSemana = movimientos
                    .Where(m => {
                        var fechaLocal = m.Fecha.ToLocalTime().Date;
                        return fechaLocal >= hace7Dias && fechaLocal <= hoy;
                    })
                    .ToList();

                // Agrupar por día
                var resumenPorDia = new List<object>();

                for (int i = 0; i < 7; i++)
                {
                    var fecha = hace7Dias.AddDays(i);

                    var movimientosDia = movimientosSemana
                        .Where(m => m.Fecha.ToLocalTime().Date == fecha)
                        .ToList();

                    var ingresos = movimientosDia.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                    var egresos = movimientosDia.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);
                    var balance = ingresos - egresos;

                    var ingresosEfectivo = movimientosDia
                        .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                        .Sum(m => m.Monto);

                    var ingresosTransferencia = movimientosDia
                        .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                        .Sum(m => m.Monto);

                    var productosVendidos = movimientosDia
                        .Where(m => m.Tipo == "ingreso" && m.Categoria == "producto")
                        .Sum(m => m.Monto);

                    var cantidadMovimientos = movimientosDia.Count;

                    // Nombre del día en español
                    var cultura = new System.Globalization.CultureInfo("es-ES");
                    var diaNombre = fecha.ToString("dddd", cultura);
                    diaNombre = char.ToUpper(diaNombre[0]) + diaNombre.Substring(1);

                    resumenPorDia.Add(new
                    {
                        fecha = fecha.ToString("yyyy-MM-dd"),
                        diaNombre = diaNombre,
                        ingresos = ingresos,
                        egresos = egresos,
                        balance = balance,
                        ingresosEfectivo = ingresosEfectivo,
                        ingresosTransferencia = ingresosTransferencia,
                        productosVendidos = productosVendidos,
                        cantidadMovimientos = cantidadMovimientos
                    });
                }

                // Calcular totales generales
                var totales = new
                {
                    ingresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                    egresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                    balance = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) -
                              movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                    productosVendidos = movimientos
                        .Where(m => m.Tipo == "ingreso" && m.Categoria == "producto")
                        .Sum(m => m.Monto),
                    cantidadMovimientos = movimientos.Count
                };

                return Ok(new
                {
                    success = true,
                    periodo = new
                    {
                        desde = hace7Dias.ToString("yyyy-MM-dd"),
                        hasta = hoy.ToString("yyyy-MM-dd")
                    },
                    resumenPorDia = resumenPorDia,
                    totales = totales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen semanal");
                return Ok(new
                {
                    success = false,
                    message = "Error al obtener resumen semanal",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener movimientos pendientes SOLO del día actual
        /// Endpoint: GET /api/caja/hoy
        /// </summary>
        [HttpGet("hoy")]
        public async Task<IActionResult> GetMovimientosHoy()
        {
            try
            {
                // CORRECCIÓN: Usar DateTime.Now.Date sin conversiones a UTC
                var hoy = DateTime.Now.Date;
                var movimientosHoy = await ObtenerMovimientosPendientesPorFechaAsync(hoy);

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
                    fecha = hoy,
                    resumen = new
                    {
                        totalIngresos = ingresos,
                        totalEgresos = egresos,
                        balance,
                        efectivo = new
                        {
                            ingresos = ingresosEfectivo,
                            egresos = egresosEfectivo,
                            balance = ingresosEfectivo - egresosEfectivo
                        },
                        transferencia = new
                        {
                            ingresos = ingresosTransferencia,
                            egresos = egresosTransferencia,
                            balance = ingresosTransferencia - egresosTransferencia
                        }
                    },
                    movimientos = movimientosHoy.Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        m.Fecha,
                        m.Cerrado
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos del día");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos del día",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener movimientos pendientes de CUALQUIER fecha (para cerrar días anteriores)
        /// Endpoint: GET /api/caja/pendientes
        /// </summary>
        [HttpGet("pendientes")]
        public async Task<IActionResult> GetMovimientosPendientes()
        {
            try
            {
                var movimientosPendientes = await ObtenerTodosMovimientosPendientesAsync();

                // Agrupar por fecha
                var movimientosPorFecha = movimientosPendientes
                    .GroupBy(m => m.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        fecha = g.Key,
                        cantidad = g.Count(),
                        totalIngresos = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                        totalEgresos = g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        balance = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) -
                                  g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto)
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    cantidadTotal = movimientosPendientes.Count,
                    diasConMovimientos = movimientosPorFecha.Count,
                    movimientosPorFecha = movimientosPorFecha
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos pendientes");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener movimientos pendientes",
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
            [FromQuery] string? desde = null,
            [FromQuery] string? hasta = null,
            [FromQuery] bool soloPendientes = false)
        {
            try
            {
                // Obtener todos los movimientos o solo pendientes
                var query = _context.MovimientosCaja.AsQueryable();

                if (soloPendientes)
                {
                    query = query.Where(m => !m.Cerrado);
                }

                var todosMovimientos = await query
                    .OrderByDescending(m => m.Fecha)
                    .ToListAsync();

                // Filtrar por fechas en memoria usando fecha local
                var movimientos = todosMovimientos;

                if (!string.IsNullOrEmpty(desde))
                {
                    var fechaDesde = DateTime.Parse(desde).Date;
                    movimientos = movimientos
                        .Where(m => m.Fecha.ToLocalTime().Date >= fechaDesde)
                        .ToList();
                }

                if (!string.IsNullOrEmpty(hasta))
                {
                    var fechaHasta = DateTime.Parse(hasta).Date;
                    movimientos = movimientos
                        .Where(m => m.Fecha.ToLocalTime().Date <= fechaHasta)
                        .ToList();
                }

                var resultado = movimientos
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        m.Fecha,
                        m.Cerrado,
                        m.CierreCajaId
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    filtros = new
                    {
                        fechaInicio = desde,
                        fechaFin = hasta,
                        soloPendientes = soloPendientes
                    },
                    cantidad = resultado.Count,
                    data = resultado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos");
                return Ok(new
                {
                    success = false,
                    message = "Error al obtener movimientos",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener historial de cierres
        /// Endpoint: GET /api/caja/cierres
        /// </summary>
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
                        c.Observaciones
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = cierres
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cierres");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener cierres",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener detalles de un cierre específico con sus movimientos
        /// Endpoint: GET /api/caja/cierres/{id}
        /// </summary>
        [HttpGet("cierres/{id}")]
        public async Task<IActionResult> GetCierreDetalle(Guid id)
        {
            try
            {
                var cierre = await _context.CierresCaja
                    .Where(c => c.Id == id)
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
                        c.Observaciones
                    })
                    .FirstOrDefaultAsync();

                if (cierre == null)
                {
                    return NotFound(new { success = false, message = "Cierre no encontrado" });
                }

                // Obtener movimientos de este cierre
                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.CierreCajaId == id)
                    .OrderBy(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        m.MetodoPago,
                        m.Fecha
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    cierre,
                    movimientos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle del cierre");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener detalle del cierre",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar ingreso manual
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
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Ingreso registrado: {categoria} - ${monto}");

                return Ok(new
                {
                    success = true,
                    message = "Ingreso registrado correctamente",
                    data = movimiento
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar ingreso");
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar ingreso",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar egreso manual
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
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Egreso registrado: {categoria} - ${monto}");

                return Ok(new
                {
                    success = true,
                    message = "Egreso registrado correctamente",
                    data = movimiento
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar egreso");
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar egreso",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Realizar cierre de caja de una fecha específica
        /// Endpoint: POST /api/caja/cerrar
        /// </summary>
        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarCaja([FromBody] CerrarCajaRequest request)
        {
            try
            {
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // CORRECCIÓN: Parsear fecha sin conversiones a UTC
                DateTime fechaCierre;
                if (string.IsNullOrEmpty(request.Fecha))
                {
                    fechaCierre = DateTime.Now.Date;
                }
                else
                {
                    fechaCierre = DateTime.Parse(request.Fecha).Date;
                }

                _logger.LogInformation($"🔒 Iniciando cierre de caja para {fechaCierre:yyyy-MM-dd}");

                // Obtener movimientos pendientes de esa fecha específica
                var movimientosPendientes = await ObtenerMovimientosPendientesPorFechaAsync(fechaCierre);

                if (!movimientosPendientes.Any())
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"No hay movimientos pendientes para el {fechaCierre:dd/MM/yyyy}"
                    });
                }

                // Calcular totales
                var ingresosEfectivo = movimientosPendientes
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivo = movimientosPendientes
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var ingresosTransferencia = movimientosPendientes
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var egresosTransferencia = movimientosPendientes
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var totalIngresos = movimientosPendientes.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientosPendientes.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                // Calcular efectivo inicial (del cierre anterior o 0)
                var cierreAnterior = await _context.CierresCaja
                    .Where(c => c.FechaCierre < fechaCierre)
                    .OrderByDescending(c => c.FechaCierre)
                    .FirstOrDefaultAsync();

                decimal efectivoInicial = cierreAnterior?.EfectivoFinal ?? 0;

                // Calcular efectivo final
                var efectivoFinal = efectivoInicial + ingresosEfectivo - egresosEfectivo;

                // Determinar cuánto efectivo dejar para el día siguiente
                var efectivoADejar = request.EfectivoADejar ?? 0;
                var efectivoACajaFuerte = efectivoFinal - efectivoADejar;

                if (efectivoACajaFuerte < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"No hay suficiente efectivo. Efectivo final: ${efectivoFinal:N2}, solicitado dejar: ${efectivoADejar:N2}"
                    });
                }

                // Crear registro de cierre
                var cierre = new CierreCaja
                {
                    Id = Guid.NewGuid(),
                    FechaCierre = DateTime.Now,
                    TipoCierre = "diario",
                    EfectivoInicial = efectivoInicial,
                    IngresosEfectivo = ingresosEfectivo,
                    EgresosEfectivo = egresosEfectivo,
                    EfectivoFinal = efectivoADejar,
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

                // Marcar movimientos como cerrados
                foreach (var movimiento in movimientosPendientes)
                {
                    movimiento.Cerrado = true;
                    movimiento.CierreCajaId = cierre.Id;
                }

                await _context.SaveChangesAsync();

                // ✅ CORRECCIÓN: Llamar directamente al método interno de CajaFuerteController
                // en vez de hacer HTTP a sí mismo (que puede fallar por HTTPS/localhost)
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
                            _logger.LogWarning($"⚠️ El cierre {cierre.Id} no pudo enviarse a Caja Fuerte (posible duplicado)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar dinero a Caja Fuerte desde cierre");
                    }
                }

                _logger.LogInformation($"Cierre completado. ID: {cierre.Id} - Fecha: {fechaCierre:dd/MM/yyyy} - Movimientos: {movimientosPendientes.Count}");

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
                        enviadoACajaFuerte = efectivoACajaFuerte > 0 || (ingresosTransferencia - egresosTransferencia) > 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al realizar cierre de caja");
                return StatusCode(500, new { success = false, message = "Error al realizar cierre de caja", error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar movimiento (SOLO si NO está cerrado)
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

                // VALIDAR: No se puede eliminar un movimiento ya cerrado
                if (movimiento.Cerrado)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se puede eliminar un movimiento que ya fue cerrado"
                    });
                }

                _context.MovimientosCaja.Remove(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Movimiento eliminado: {id}");

                return Ok(new { success = true, message = "Movimiento eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar movimiento");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al eliminar movimiento",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Recibir dinero desde Caja Fuerte (transferencia interna)
        /// Endpoint: POST /api/caja/recibir-desde-cajafuerte
        /// </summary>
        [HttpPost("recibir-desde-cajafuerte")]
        public async Task<IActionResult> RecibirDesdeCajaFuerte([FromBody] RecibirDesdeCajaFuerteRequest request)
        {
            try
            {
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // Crear movimiento de INGRESO en Caja Diaria
                var movimiento = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = "transferencia_cajafuerte",
                    MetodoPago = "efectivo",
                    Monto = request.Monto,
                    Descripcion = "Transferencia desde Caja Fuerte",
                    ReferenciaId = null,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CierreCajaId = null,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"💰 Dinero recibido desde Caja Fuerte: ${request.Monto}");

                return Ok(new
                {
                    success = true,
                    message = $"${request.Monto:N2} recibidos desde Caja Fuerte",
                    movimiento = new
                    {
                        movimiento.Id,
                        movimiento.Monto,
                        movimiento.Descripcion,
                        movimiento.Fecha
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recibir dinero desde Caja Fuerte");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al recibir dinero desde Caja Fuerte"
                });
            }
        }

        public class CerrarCajaRequest
        {
            public string Fecha { get; set; }
            public string Observaciones { get; set; }
            public decimal? EfectivoADejar { get; set; }
        }

        public class RecibirDesdeCajaFuerteRequest
        {
            public decimal Monto { get; set; }
        }

    }
}