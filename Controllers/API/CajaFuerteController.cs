using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Security.Cryptography;
using System.Text;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class CajaFuerteController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CajaFuerteController> _logger;

        public CajaFuerteController(AppDbContext context, ILogger<CajaFuerteController> logger)
        {
            _context = context;
            _logger = logger;
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

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public async Task<CajaFuerte> ObtenerOCrearCajaFuerteAsync()
        {
            var cajaFuerte = await _context.CajasFuertes.FirstOrDefaultAsync();

            if (cajaFuerte == null)
            {
                cajaFuerte = new CajaFuerte
                {
                    Id = Guid.NewGuid(),
                    BalanceEfectivo = 0,
                    BalanceTransferencias = 0,
                    BalanceTotal = 0,
                    UltimaActualizacion = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.CajasFuertes.Add(cajaFuerte);
                await _context.SaveChangesAsync();
            }

            return cajaFuerte;
        }

        private async Task<ConfiguracionCajaFuerte> ObtenerOCrearConfiguracionAsync()
        {
            var config = await _context.ConfiguracionesCajaFuerte.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionCajaFuerte
                {
                    Id = Guid.NewGuid(),
                    PasswordHash = HashPassword("123456"),
                    RequiereCambioPassword = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.ConfiguracionesCajaFuerte.Add(config);
                await _context.SaveChangesAsync();
            }

            return config;
        }

        // ✅ MÉTODO PÚBLICO para que CajaController lo llame directamente
        // Recibe el monto neto de efectivo y transferencias de un cierre y lo acredita a Caja Fuerte
        public async Task<bool> RecibirDesdeCajaInternoAsync(
            decimal montoEfectivo, decimal montoTransferencia,
            DateTime fechaCierre, Guid cierreCajaId)
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // Verificar duplicado
                var yaExiste = await _context.MovimientosCajaFuerte
                    .AnyAsync(m => m.CierreCajaId == cierreCajaId && m.Origen == "cierre_caja");

                if (yaExiste)
                {
                    _logger.LogWarning($"El cierre {cierreCajaId} ya fue procesado en Caja Fuerte");
                    return false;
                }

                int creados = 0;

                if (montoEfectivo > 0)
                {
                    _context.MovimientosCajaFuerte.Add(new MovimientoCajaFuerte
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Origen = "cierre_caja",
                        MetodoPago = "efectivo",
                        Monto = montoEfectivo,
                        Descripcion = $"Cierre de caja del {fechaCierre:dd/MM/yyyy} - Efectivo",
                        CierreCajaId = cierreCajaId,
                        UsuarioId = usuarioId,
                        Fecha = fechaCierre == default ? DateTime.Now : fechaCierre,
                        CreatedAt = DateTime.Now
                    });
                    cajaFuerte.BalanceEfectivo += montoEfectivo;
                    creados++;
                }

                if (montoTransferencia > 0)
                {
                    _context.MovimientosCajaFuerte.Add(new MovimientoCajaFuerte
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Origen = "cierre_caja",
                        MetodoPago = "transferencia",
                        Monto = montoTransferencia,
                        Descripcion = $"Cierre de caja del {fechaCierre:dd/MM/yyyy} - Transferencias",
                        CierreCajaId = cierreCajaId,
                        UsuarioId = usuarioId,
                        Fecha = fechaCierre == default ? DateTime.Now : fechaCierre,
                        CreatedAt = DateTime.Now
                    });
                    cajaFuerte.BalanceTransferencias += montoTransferencia;
                    creados++;
                }

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"✅ Cierre recibido en Caja Fuerte | Efectivo: ${montoEfectivo} | Transferencias: ${montoTransferencia} | Balance total: ${cajaFuerte.BalanceTotal} | Movimientos creados: {creados}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RecibirDesdeCajaInternoAsync");
                return false;
            }
        }

        private static string ObtenerNombreMes(int mes) => mes switch
        {
            1 => "Enero",
            2 => "Febrero",
            3 => "Marzo",
            4 => "Abril",
            5 => "Mayo",
            6 => "Junio",
            7 => "Julio",
            8 => "Agosto",
            9 => "Septiembre",
            10 => "Octubre",
            11 => "Noviembre",
            12 => "Diciembre",
            _ => "Desconocido"
        };

        private static string ObtenerOrigenLabel(string origen) => origen switch
        {
            "cierre_caja" => "Cierre de Caja",
            "retiro_manual" => "Retiro Manual",
            "transferencia_a_caja" => "Transferencia a Caja Diaria",
            "transferencia_desde_caja" => "Transferencia desde Caja Diaria",
            "ingreso_manual" => "Ingreso Manual",
            _ => origen ?? "Otro"
        };

        // =============================================
        // AUTENTICACIÓN
        // =============================================

        [HttpPost("verificar-password")]
        public async Task<IActionResult> VerificarPassword([FromBody] VerificarPasswordRequest request)
        {
            try
            {
                var config = await ObtenerOCrearConfiguracionAsync();
                var passwordHash = HashPassword(request.Password);

                if (passwordHash == config.PasswordHash)
                {
                    return Ok(new
                    {
                        success = true,
                        requiereCambio = config.RequiereCambioPassword,
                        message = "Contraseña correcta"
                    });
                }

                return Ok(new { success = false, message = "Contraseña incorrecta" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar contraseña");
                return StatusCode(500, new { success = false, message = "Error al verificar contraseña" });
            }
        }

        [HttpPost("cambiar-password")]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
        {
            try
            {
                var config = await ObtenerOCrearConfiguracionAsync();
                var passwordActualHash = HashPassword(request.PasswordActual);

                if (passwordActualHash != config.PasswordHash)
                    return Ok(new { success = false, message = "Contraseña actual incorrecta" });

                config.PasswordHash = HashPassword(request.PasswordNueva);
                config.RequiereCambioPassword = false;
                config.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Contraseña cambiada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar contraseña");
                return StatusCode(500, new { success = false, message = "Error al cambiar contraseña" });
            }
        }

        // =============================================
        // BALANCES (reemplaza al dashboard eliminado)
        // =============================================

        /// <summary>
        /// Devuelve solo los balances acumulados de la Caja Fuerte.
        /// Endpoint: GET /api/cajafuerte/balances
        /// </summary>
        [HttpGet("balances")]
        public async Task<IActionResult> GetBalances()
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();

                return Ok(new
                {
                    success = true,
                    efectivo = cajaFuerte.BalanceEfectivo,
                    transferencias = cajaFuerte.BalanceTransferencias,
                    total = cajaFuerte.BalanceTotal,
                    ultimaActualizacion = cajaFuerte.UltimaActualizacion
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener balances de Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener balances" });
            }
        }

        // =============================================
        // MOVIMIENTOS DE CAJA DIARIA (detalle de ventas)
        // =============================================

        /// <summary>
        /// Devuelve los movimientos de Caja Diaria (ventas reales) para un rango de fechas o por cierre.
        /// Se usa para mostrar el detalle de lo vendido dentro del acordeón de cada cierre en Caja Fuerte.
        /// Endpoint: GET /api/cajafuerte/movimientos-diarios?desde=&hasta=
        ///           GET /api/cajafuerte/movimientos-diarios?cierreId=
        /// </summary>
        [HttpGet("movimientos-diarios")]
        public async Task<IActionResult> GetMovimientosDiarios(
            [FromQuery] string? desde = null,
            [FromQuery] string? hasta = null,
            [FromQuery] Guid? cierreId = null)
        {
            try
            {
                var query = _context.MovimientosCaja.AsQueryable();

                if (cierreId.HasValue)
                {
                    // Filtrar exactamente por CierreCajaId
                    query = query.Where(m => m.CierreCajaId == cierreId.Value);
                }
                else
                {
                    // Filtrar por rango de fechas en memoria
                    var todos = await query.ToListAsync();

                    if (!string.IsNullOrEmpty(desde))
                    {
                        var fechaDesde = DateTime.Parse(desde).Date;
                        todos = todos.Where(m => m.Fecha.ToLocalTime().Date >= fechaDesde).ToList();
                    }
                    if (!string.IsNullOrEmpty(hasta))
                    {
                        var fechaHasta = DateTime.Parse(hasta).Date;
                        todos = todos.Where(m => m.Fecha.ToLocalTime().Date <= fechaHasta).ToList();
                    }

                    var totalIngresos = todos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                    var totalEgresos = todos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                    return Ok(new
                    {
                        success = true,
                        cantidad = todos.Count,
                        totalIngresos,
                        totalEgresos,
                        balance = totalIngresos - totalEgresos,
                        data = todos.OrderBy(m => m.Fecha).Select(m => new
                        {
                            m.Id,
                            m.Tipo,
                            categoria = m.Categoria,
                            m.Monto,
                            m.Descripcion,
                            metodoPago = m.MetodoPago,
                            fecha = m.Fecha.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                            m.Cerrado,
                            cierreCajaId = m.CierreCajaId
                        })
                    });
                }

                var movs = await query.OrderBy(m => m.Fecha).ToListAsync();
                var ingr = movs.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var egr = movs.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    cantidad = movs.Count,
                    totalIngresos = ingr,
                    totalEgresos = egr,
                    balance = ingr - egr,
                    data = movs.Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        categoria = m.Categoria,
                        m.Monto,
                        m.Descripcion,
                        metodoPago = m.MetodoPago,
                        fecha = m.Fecha.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        m.Cerrado,
                        cierreCajaId = m.CierreCajaId
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos diarios para Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos diarios" });
            }
        }

        // =============================================
        // MOVIMIENTOS - LISTADO CON FILTROS
        // =============================================

        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos(
            [FromQuery] string vista = "mensual",
            [FromQuery] DateTime? fecha = null,
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            [FromQuery] string? tipo = null,
            [FromQuery] string? metodoPago = null)
        {
            try
            {
                var fechaBase = (fecha ?? DateTime.Now).ToLocalTime().Date;
                DateTime fechaDesde, fechaHasta;

                if (desde.HasValue && hasta.HasValue)
                {
                    fechaDesde = desde.Value.Date;
                    fechaHasta = hasta.Value.Date.AddDays(1);
                    vista = "rango";
                }
                else
                {
                    switch (vista.ToLower())
                    {
                        case "diario":
                            fechaDesde = fechaBase;
                            fechaHasta = fechaBase.AddDays(1);
                            break;
                        case "semanal":
                            var diff = (7 + (fechaBase.DayOfWeek - DayOfWeek.Monday)) % 7;
                            fechaDesde = fechaBase.AddDays(-diff);
                            fechaHasta = fechaDesde.AddDays(7);
                            break;
                        case "mensual":
                        default:
                            fechaDesde = new DateTime(fechaBase.Year, fechaBase.Month, 1);
                            fechaHasta = fechaDesde.AddMonths(1);
                            break;
                    }
                }

                var query = await _context.MovimientosCajaFuerte.ToListAsync();

                var movimientosFiltrados = query
                    .Where(m =>
                    {
                        var fechaLocal = m.Fecha.ToLocalTime().Date;
                        return fechaLocal >= fechaDesde && fechaLocal < fechaHasta;
                    })
                    .ToList();

                if (!string.IsNullOrEmpty(tipo))
                    movimientosFiltrados = movimientosFiltrados.Where(m => m.Tipo == tipo).ToList();

                if (!string.IsNullOrEmpty(metodoPago))
                    movimientosFiltrados = movimientosFiltrados.Where(m => m.MetodoPago == metodoPago).ToList();

                var ingresosEfectivo = movimientosFiltrados.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientosFiltrados.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientosFiltrados.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientosFiltrados.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var totalIngresos = ingresosEfectivo + ingresosTransferencia;
                var totalEgresos = egresosEfectivo + egresosTransferencia;

                // ✅ CORRECCIÓN: serializar fecha como string "yyyy-MM-dd" para evitar
                // problemas de parseo UTC/Local en el frontend
                var movimientosProyectados = movimientosFiltrados
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Origen,
                        origenLabel = ObtenerOrigenLabel(m.Origen),
                        m.MetodoPago,
                        m.Monto,
                        m.Descripcion,
                        m.Categoria,
                        fecha = m.Fecha.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        esCierreCaja = m.Origen == "cierre_caja",
                        cierreCajaId = m.CierreCajaId, // ✅ Para cargar detalle de ventas
                        m.CreatedAt
                    }).ToList();

                // ✅ CORRECCIÓN: fecha del grupo como string "yyyy-MM-dd"
                var movimientosPorDia = movimientosFiltrados
                    .GroupBy(m => m.Fecha.ToLocalTime().Date)
                    .Select(g => new
                    {
                        fecha = g.Key.ToString("yyyy-MM-dd"), // ✅ string puro, sin ambigüedad UTC
                        movimientos = g.OrderByDescending(m => m.Fecha).Select(m => new
                        {
                            m.Id,
                            m.Tipo,
                            m.Origen,
                            origenLabel = ObtenerOrigenLabel(m.Origen),
                            m.MetodoPago,
                            m.Monto,
                            m.Descripcion,
                            m.Categoria,
                            fecha = m.Fecha.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                            esCierreCaja = m.Origen == "cierre_caja",
                            cierreCajaId = m.CierreCajaId // ✅ Para cargar detalle de ventas del cierre
                        }).ToList(),
                        ingresosEfectivo = g.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        egresosEfectivo = g.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        ingresosTransferencia = g.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                        egresosTransferencia = g.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                        totalIngresos = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto),
                        totalEgresos = g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        balance = g.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto) - g.Where(m => m.Tipo == "egreso").Sum(m => m.Monto),
                        cantidad = g.Count()
                    })
                    .OrderByDescending(d => d.fecha)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    vista,
                    periodo = new
                    {
                        desde = fechaDesde.ToString("yyyy-MM-dd"),
                        hasta = fechaHasta.AddDays(-1).ToString("yyyy-MM-dd")
                    },
                    movimientos = movimientosProyectados,
                    movimientosPorDia,
                    totales = new
                    {
                        ingresosEfectivo,
                        egresosEfectivo,
                        balanceEfectivo = ingresosEfectivo - egresosEfectivo,
                        ingresosTransferencia,
                        egresosTransferencia,
                        balanceTransferencia = ingresosTransferencia - egresosTransferencia,
                        totalIngresos,
                        totalEgresos,
                        balance = totalIngresos - totalEgresos,
                        cantidad = movimientosFiltrados.Count,
                        diasConMovimientos = movimientosPorDia.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos de Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos" });
            }
        }

        // =============================================
        // REGISTRAR EGRESO MANUAL
        // =============================================

        [HttpPost("registrar-egreso")]
        public async Task<IActionResult> RegistrarEgreso([FromBody] RegistrarEgresoRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                if (request.MetodoPago == "efectivo" && cajaFuerte.BalanceEfectivo < request.Monto)
                    return BadRequest(new { success = false, message = $"Saldo insuficiente en efectivo. Disponible: ${cajaFuerte.BalanceEfectivo:N0}" });

                if (request.MetodoPago == "transferencia" && cajaFuerte.BalanceTransferencias < request.Monto)
                    return BadRequest(new { success = false, message = $"Saldo insuficiente en transferencias. Disponible: ${cajaFuerte.BalanceTransferencias:N0}" });

                var movimiento = new MovimientoCajaFuerte
                {
                    Id = Guid.NewGuid(),
                    Tipo = "egreso",
                    Origen = "retiro_manual",
                    MetodoPago = request.MetodoPago,
                    Monto = request.Monto,
                    Descripcion = request.Descripcion,
                    Categoria = request.Categoria,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCajaFuerte.Add(movimiento);

                if (request.MetodoPago == "efectivo")
                    cajaFuerte.BalanceEfectivo -= request.Monto;
                else
                    cajaFuerte.BalanceTransferencias -= request.Monto;

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Egreso CF: ${request.Monto} ({request.MetodoPago}) - {request.Categoria}");

                return Ok(new
                {
                    success = true,
                    message = "Egreso registrado correctamente",
                    movimiento = new { movimiento.Id, movimiento.Monto, movimiento.MetodoPago, movimiento.Descripcion, movimiento.Categoria },
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar egreso en Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al registrar egreso" });
            }
        }

        // =============================================
        // REGISTRAR INGRESO MANUAL
        // =============================================

        [HttpPost("registrar-ingreso")]
        public async Task<IActionResult> RegistrarIngreso([FromBody] RegistrarIngresoRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                var movimiento = new MovimientoCajaFuerte
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Origen = "ingreso_manual",
                    MetodoPago = request.MetodoPago,
                    Monto = request.Monto,
                    Descripcion = request.Descripcion,
                    Categoria = request.Categoria,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCajaFuerte.Add(movimiento);

                if (request.MetodoPago == "efectivo")
                    cajaFuerte.BalanceEfectivo += request.Monto;
                else
                    cajaFuerte.BalanceTransferencias += request.Monto;

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Ingreso CF manual: ${request.Monto} ({request.MetodoPago})");

                return Ok(new
                {
                    success = true,
                    message = "Ingreso registrado correctamente",
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar ingreso en Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al registrar ingreso" });
            }
        }

        // =============================================
        // TRANSFERIR A CAJA DIARIA (Caja Fuerte → Caja Diaria)
        // =============================================

        [HttpPost("transferir-a-caja")]
        public async Task<IActionResult> TransferirACaja([FromBody] TransferirACajaRequest request)
        {
            try
            {
                if (request.Monto <= 0)
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                if (cajaFuerte.BalanceEfectivo < request.Monto)
                    return BadRequest(new { success = false, message = $"Saldo insuficiente en efectivo. Disponible: ${cajaFuerte.BalanceEfectivo:N0}" });

                // EGRESO en Caja Fuerte
                var movimientoCF = new MovimientoCajaFuerte
                {
                    Id = Guid.NewGuid(),
                    Tipo = "egreso",
                    Origen = "transferencia_a_caja",
                    MetodoPago = "efectivo",
                    Monto = request.Monto,
                    Descripcion = $"Transferencia a Caja Diaria{(string.IsNullOrEmpty(request.Descripcion) ? "" : $": {request.Descripcion}")}",
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    CreatedAt = DateTime.Now
                };
                _context.MovimientosCajaFuerte.Add(movimientoCF);

                cajaFuerte.BalanceEfectivo -= request.Monto;
                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                // INGRESO en Caja Diaria
                var movimientoCaja = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "ingreso",
                    Categoria = "transferencia_cajafuerte",
                    MetodoPago = "efectivo",
                    Monto = request.Monto,
                    Descripcion = $"Transferencia desde Caja Fuerte{(string.IsNullOrEmpty(request.Descripcion) ? "" : $": {request.Descripcion}")}",
                    ReferenciaId = null,
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    Cerrado = false,
                    CierreCajaId = null,
                    CreatedAt = DateTime.Now
                };
                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Transferencia CF → Caja Diaria: ${request.Monto}");

                return Ok(new
                {
                    success = true,
                    message = $"${request.Monto:N0} transferidos a Caja Diaria exitosamente",
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al transferir a Caja Diaria");
                return StatusCode(500, new { success = false, message = "Error al realizar transferencia" });
            }
        }

        // =============================================
        // RECIBIR DESDE CAJA (endpoint HTTP - respaldo)
        // El método principal es RecibirDesdeCajaInternoAsync
        // =============================================

        [HttpPost("recibir-desde-caja")]
        public async Task<IActionResult> RecibirDesdeCaja([FromBody] RecibirDesdeCajaRequest request)
        {
            try
            {
                var ok = await RecibirDesdeCajaInternoAsync(
                    request.MontoEfectivo,
                    request.MontoTransferencia,
                    request.FechaCierre,
                    request.CierreCajaId);

                if (!ok)
                    return Ok(new { success = false, message = "Este cierre ya fue procesado anteriormente en Caja Fuerte o no hubo montos que transferir." });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                return Ok(new
                {
                    success = true,
                    message = $"Cierre recibido: Efectivo ${request.MontoEfectivo:N0}, Transferencias ${request.MontoTransferencia:N0}",
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recibir dinero desde Caja Diaria");
                return StatusCode(500, new { success = false, message = "Error al recibir dinero" });
            }
        }

        // =============================================
        // ELIMINAR MOVIMIENTO
        // =============================================

        [HttpDelete("movimientos/{id}")]
        public async Task<IActionResult> EliminarMovimiento(Guid id, [FromBody] EliminarMovimientoRequest? request = null)
        {
            try
            {
                var movimiento = await _context.MovimientosCajaFuerte.FindAsync(id);

                if (movimiento == null)
                    return NotFound(new { success = false, message = "Movimiento no encontrado" });

                // No se pueden eliminar movimientos de cierre de caja (son contables)
                if (movimiento.Origen == "cierre_caja")
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se pueden eliminar movimientos de cierre de caja. Para corregir, realice un ajuste manual."
                    });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // ✅ Revertir el balance correctamente
                if (movimiento.Tipo == "ingreso")
                {
                    if (movimiento.MetodoPago == "efectivo")
                        cajaFuerte.BalanceEfectivo -= movimiento.Monto;
                    else
                        cajaFuerte.BalanceTransferencias -= movimiento.Monto;
                }
                else // egreso
                {
                    if (movimiento.MetodoPago == "efectivo")
                        cajaFuerte.BalanceEfectivo += movimiento.Monto;
                    else
                        cajaFuerte.BalanceTransferencias += movimiento.Monto;
                }

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UpdatedAt = DateTime.Now;

                // Registrar en historial de eliminados
                var eliminado = new MovimientoEliminado
                {
                    Id = Guid.NewGuid(),
                    MovimientoOriginalId = movimiento.Id,
                    Tipo = movimiento.Tipo,
                    Origen = movimiento.Origen,
                    MetodoPago = movimiento.MetodoPago,
                    Monto = movimiento.Monto,
                    Descripcion = movimiento.Descripcion,
                    Categoria = movimiento.Categoria,
                    FechaOriginal = movimiento.Fecha,
                    FechaEliminacion = DateTime.Now,
                    UsuarioEliminacion = usuarioId,
                    MotivoEliminacion = request?.Motivo ?? "Sin motivo especificado",
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosEliminados.Add(eliminado);
                _context.MovimientosCajaFuerte.Remove(movimiento);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Movimiento CF eliminado: ${movimiento.Monto} ({movimiento.Tipo}/{movimiento.MetodoPago}). Nuevo balance: ${cajaFuerte.BalanceTotal}");

                return Ok(new
                {
                    success = true,
                    message = "Movimiento eliminado y balance actualizado",
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar movimiento");
                return StatusCode(500, new { success = false, message = "Error al eliminar movimiento" });
            }
        }

        // =============================================
        // CONSOLIDADOS HISTÓRICOS
        // =============================================

        [HttpGet("consolidados")]
        public async Task<IActionResult> GetConsolidados()
        {
            try
            {
                var consolidadosDB = await _context.ConsolidadosMensuales
                    .OrderByDescending(c => c.Anio)
                    .ThenByDescending(c => c.Mes)
                    .ToListAsync();

                var consolidados = consolidadosDB.Select(c => new
                {
                    c.Id,
                    c.Mes,
                    c.Anio,
                    mesNombre = ObtenerNombreMes(c.Mes),
                    periodo = $"{ObtenerNombreMes(c.Mes)} {c.Anio}",
                    efectivo = new { ingresos = c.TotalIngresosEfectivo, egresos = c.TotalEgresosEfectivo, balance = c.BalanceFinalEfectivo },
                    transferencias = new { ingresos = c.TotalIngresosTransferencia, egresos = c.TotalEgresosTransferencia, balance = c.BalanceFinalTransferencia },
                    totales = new
                    {
                        ingresos = c.TotalIngresosEfectivo + c.TotalIngresosTransferencia,
                        egresos = c.TotalEgresosEfectivo + c.TotalEgresosTransferencia,
                        balance = c.BalanceFinalEfectivo + c.BalanceFinalTransferencia
                    },
                    c.FechaConsolidacion
                }).ToList();

                return Ok(new { success = true, consolidados, total = consolidados.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consolidados");
                return StatusCode(500, new { success = false, message = "Error al obtener consolidados" });
            }
        }

        [HttpPost("consolidar-mes")]
        public async Task<IActionResult> ConsolidarMes([FromBody] ConsolidarMesRequest request)
        {
            try
            {
                var mes = request.Mes;
                var anio = request.Anio;

                var existeConsolidado = await _context.ConsolidadosMensuales
                    .AnyAsync(c => c.Mes == mes && c.Anio == anio);

                if (existeConsolidado)
                    return BadRequest(new { success = false, message = "Ya existe un consolidado para este mes" });

                var primerDia = new DateTime(anio, mes, 1);
                var ultimoDia = primerDia.AddMonths(1);

                var todosMovimientos = await _context.MovimientosCajaFuerte.ToListAsync();
                var movimientos = todosMovimientos
                    .Where(m => m.Fecha.ToLocalTime().Date >= primerDia && m.Fecha.ToLocalTime().Date < ultimoDia)
                    .ToList();

                var totalIngresosEfectivo = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var totalEgresosEfectivo = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var totalIngresosTransferencia = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var totalEgresosTransferencia = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                var consolidado = new ConsolidadoMensual
                {
                    Id = Guid.NewGuid(),
                    Mes = mes,
                    Anio = anio,
                    TotalIngresosEfectivo = totalIngresosEfectivo,
                    TotalIngresosTransferencia = totalIngresosTransferencia,
                    TotalEgresosEfectivo = totalEgresosEfectivo,
                    TotalEgresosTransferencia = totalEgresosTransferencia,
                    BalanceFinalEfectivo = totalIngresosEfectivo - totalEgresosEfectivo,
                    BalanceFinalTransferencia = totalIngresosTransferencia - totalEgresosTransferencia,
                    FechaConsolidacion = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.ConsolidadosMensuales.Add(consolidado);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Mes {ObtenerNombreMes(mes)} {anio} consolidado correctamente",
                    consolidado = new
                    {
                        consolidado.Id,
                        mes,
                        anio,
                        mesNombre = ObtenerNombreMes(mes),
                        movimientosProcesados = movimientos.Count,
                        totales = new
                        {
                            ingresosEfectivo = totalIngresosEfectivo,
                            egresosEfectivo = totalEgresosEfectivo,
                            ingresosTransferencia = totalIngresosTransferencia,
                            egresosTransferencia = totalEgresosTransferencia,
                            balanceEfectivo = consolidado.BalanceFinalEfectivo,
                            balanceTransferencia = consolidado.BalanceFinalTransferencia
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consolidar mes");
                return StatusCode(500, new { success = false, message = "Error al consolidar mes" });
            }
        }
    }

    // ===== REQUEST MODELS =====

    public class VerificarPasswordRequest { public string Password { get; set; } }
    public class CambiarPasswordRequest { public string PasswordActual { get; set; } public string PasswordNueva { get; set; } }

    public class RegistrarEgresoRequest
    {
        public string MetodoPago { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public string Categoria { get; set; }
    }

    public class RegistrarIngresoRequest
    {
        public string MetodoPago { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public string? Categoria { get; set; }
    }

    public class TransferirACajaRequest
    {
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
    }

    public class RecibirDesdeCajaRequest
    {
        public decimal MontoEfectivo { get; set; }
        public decimal MontoTransferencia { get; set; }
        public DateTime FechaCierre { get; set; }
        public Guid CierreCajaId { get; set; }
    }

    public class ConsolidarMesRequest { public int Mes { get; set; } public int Anio { get; set; } }

    public class EliminarMovimientoRequest { public string? Motivo { get; set; } }
}