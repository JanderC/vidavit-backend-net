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

        public CajaController(AppDbContext context, ILogger<CajaController> logger)
        {
            _context = context;
            _logger = logger;
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
            // Convertir la fecha a inicio y fin del día en UTC
            var inicioDelDia = DateTime.SpecifyKind(fecha.Date, DateTimeKind.Utc);
            var finDelDia = inicioDelDia.AddDays(1);

            _logger.LogInformation($"🔍 Buscando movimientos pendientes para {fecha:yyyy-MM-dd}");
            _logger.LogInformation($"   Rango UTC: {inicioDelDia:yyyy-MM-dd HH:mm:ss} a {finDelDia:yyyy-MM-dd HH:mm:ss}");

            // SOLUCIÓN: Usar solo la parte DATE para la comparación
            // Esto ignora completamente la hora y zona horaria
            var movimientos = await _context.MovimientosCaja
                .Where(m => !m.Cerrado)
                .ToListAsync();

            // Filtrar en memoria por la fecha (solo día, mes, año)
            var movimientosFiltrados = movimientos
                .Where(m => m.Fecha.Date == fecha.Date)
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
                var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

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
                var movimientosPendientesAnteriores = await _context.MovimientosCaja
                    .Where(m => !m.Cerrado && m.Fecha < hoy)
                    .CountAsync();

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
                    ultimoCierre,
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
                var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var hace7Dias = hoy.AddDays(-6); // Los últimos 7 días (incluyendo hoy)

                // Obtener todos los movimientos de los últimos 7 días
                var movimientos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= hace7Dias && m.Fecha < hoy.AddDays(1))
                    .ToListAsync();

                // Agrupar por día
                var resumenPorDia = new List<object>();

                for (int i = 0; i < 7; i++)
                {
                    var fecha = hace7Dias.AddDays(i);
                    var inicioDelDia = fecha;
                    var finDelDia = fecha.AddDays(1);

                    var movimientosDia = movimientos
                        .Where(m => m.Fecha >= inicioDelDia && m.Fecha < finDelDia)
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
                var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
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

                // Agrupar por fecha para mostrar días con movimientos pendientes
                var porFecha = movimientosPendientes
                    .GroupBy(m => m.Fecha.Date)
                    .Select(g => new
                    {
                        fecha = g.Key,
                        cantidadMovimientos = g.Count(),
                        totalIngresos = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                        totalEgresos = g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        balance = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) -
                                 g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        movimientos = g.Select(m => new
                        {
                            m.Id,
                            m.Tipo,
                            m.Categoria,
                            m.Monto,
                            m.Descripcion,
                            m.MetodoPago,
                            m.Fecha
                        }).ToList()
                    })
                    .OrderBy(g => g.fecha)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    totalDiasPendientes = porFecha.Count,
                    diasPendientes = porFecha
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
        /// Obtener todos los movimientos con filtros opcionales
        /// Endpoint: GET /api/caja/movimientos
        /// </summary>
        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
           [FromQuery] string? desde = null,
           [FromQuery] string? hasta = null,
           [FromQuery] string? categoria = null,
           [FromQuery] bool? soloPendientes = null)
        {
            try
            {
                _logger.LogInformation($"📋 Obteniendo movimientos - Desde: {desde}, Hasta: {hasta}, Categoría: {categoria}");

                // Iniciar con todos los movimientos
                var query = _context.MovimientosCaja.AsQueryable();

                // Filtrar solo pendientes
                if (soloPendientes.HasValue && soloPendientes.Value)
                {
                    query = query.Where(m => !m.Cerrado);
                }

                // Traer a memoria para filtrar por fecha (evita problemas de zona horaria)
                var movimientos = await query.ToListAsync();

                // Filtrar por fecha desde
                if (!string.IsNullOrEmpty(desde) && DateTime.TryParse(desde, out var fechaDesde))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date >= fechaDesde.Date).ToList();
                    _logger.LogInformation($"📅 Filtrado desde {fechaDesde:yyyy-MM-dd}");
                }

                // Filtrar por fecha hasta
                if (!string.IsNullOrEmpty(hasta) && DateTime.TryParse(hasta, out var fechaHasta))
                {
                    movimientos = movimientos.Where(m => m.Fecha.Date <= fechaHasta.Date).ToList();
                    _logger.LogInformation($"📅 Filtrado hasta {fechaHasta:yyyy-MM-dd}");
                }

                // Filtrar por categoría
                if (!string.IsNullOrEmpty(categoria))
                {
                    movimientos = movimientos.Where(m => m.Categoria == categoria).ToList();
                    _logger.LogInformation($"🏷️ Filtrado por categoría: {categoria}");
                }

                // Ordenar por fecha descendente
                movimientos = movimientos.OrderByDescending(m => m.Fecha).ToList();

                _logger.LogInformation($"✅ Encontrados {movimientos.Count} movimientos");

                return Ok(new
                {
                    success = true,
                    filtros = new
                    {
                        fechaInicio = desde,
                        fechaFin = hasta,
                        soloPendientes = soloPendientes
                    },
                    cantidad = movimientos.Count,
                    data = movimientos
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
                    Fecha = DateTime.UtcNow,
                    Cerrado = false,
                    CreatedAt = DateTime.UtcNow
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
                    Fecha = DateTime.UtcNow,
                    Cerrado = false,
                    CreatedAt = DateTime.UtcNow
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
        /// Cierre de caja - puede ser del día actual o de días anteriores
        /// Endpoint: POST /api/caja/cerrar
        /// Body (opcional): { "fecha": "2025-01-15", "observaciones": "..." }
        /// Si no se envía fecha, cierra el día actual
        /// </summary>
        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarCaja([FromBody] JsonElement data)
        {
            try
            {
                // Obtener fecha a cerrar (si no se especifica, usar hoy)
                DateTime fechaCierre = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                if (data.TryGetProperty("fecha", out var fechaEl))
                {
                    var fechaStr = fechaEl.GetString();
                    _logger.LogInformation($"📅 Fecha recibida del cliente: {fechaStr}");

                    // Parsear la fecha y asegurarse que es UTC
                    fechaCierre = DateTime.SpecifyKind(DateTime.Parse(fechaStr).Date, DateTimeKind.Utc);

                    _logger.LogInformation($"📅 Fecha procesada para cierre: {fechaCierre:yyyy-MM-dd HH:mm:ss} UTC");
                }

                var observaciones = "";
                if (data.TryGetProperty("observaciones", out var obsEl))
                {
                    observaciones = obsEl.GetString();
                }

                _logger.LogInformation($"Iniciando cierre de caja para fecha: {fechaCierre:dd/MM/yyyy}");

                // Validar que la fecha no sea futura
                if (fechaCierre > DateTime.UtcNow.Date)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se puede cerrar una fecha futura"
                    });
                }

                // Obtener SOLO movimientos NO cerrados de la fecha específica
                var movimientosPendientes = await ObtenerMovimientosPendientesPorFechaAsync(fechaCierre);

                if (movimientosPendientes.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"No hay movimientos pendientes para cerrar en la fecha {fechaCierre:dd/MM/yyyy}"
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

                var efectivoInicial = 0;
                var efectivoFinal = ingresosEfectivo - egresosEfectivo;
                var balanceGeneral = totalIngresos - totalEgresos;

                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // Crear el cierre
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
                    CantidadMovimientos = movimientosPendientes.Count,
                    Observaciones = $"{observaciones} - Cierre del día {fechaCierre:dd/MM/yyyy}".Trim(),
                    UsuarioId = usuarioId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CierresCaja.Add(cierre);
                await _context.SaveChangesAsync();

                // MARCAR TODOS LOS MOVIMIENTOS COMO CERRADOS
                foreach (var movimiento in movimientosPendientes)
                {
                    movimiento.Cerrado = true;
                    movimiento.CierreCajaId = cierre.Id;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Cierre completado. ID: {cierre.Id} - Fecha: {fechaCierre:dd/MM/yyyy} - Movimientos: {movimientosPendientes.Count}");

                // Verificar si quedan días pendientes
                var diasPendientesRestantes = await _context.MovimientosCaja
                    .Where(m => !m.Cerrado)
                    .Select(m => m.Fecha.Date)
                    .Distinct()
                    .CountAsync();

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
                    },
                    alertas = new
                    {
                        diasPendientesRestantes,
                        tieneDiasPendientes = diasPendientesRestantes > 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al realizar cierre de caja");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al realizar cierre de caja",
                    error = ex.Message
                });
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
    }
}