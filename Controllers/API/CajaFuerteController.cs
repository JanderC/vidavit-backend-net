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

        // =============================================
        // MÉTODO PÚBLICO - Recibir cierre desde CajaController
        // =============================================

        /// <summary>
        /// Llamado internamente desde CajaController al cerrar caja.
        /// Recibe el efectivo neto (ya descontado lo que se dejó para el día siguiente)
        /// y las transferencias netas. Crea los movimientos en Caja Fuerte y actualiza el balance.
        /// </summary>
        public async Task<bool> RecibirDesdeCajaInternoAsync(
            decimal montoEfectivo, decimal montoTransferencia,
            DateTime fechaCierre, Guid cierreCajaId)
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // Verificar duplicado — un cierre solo puede entrar a CF una vez
                var yaExiste = await _context.MovimientosCajaFuerte
                    .AnyAsync(m => m.CierreCajaId == cierreCajaId && m.Origen == "cierre_caja");

                if (yaExiste)
                {
                    _logger.LogWarning($"El cierre {cierreCajaId} ya fue procesado en Caja Fuerte");
                    return false;
                }

                // Si ambos montos son 0, no hay nada que registrar
                if (montoEfectivo == 0 && montoTransferencia == 0)
                {
                    _logger.LogInformation($"Cierre {cierreCajaId}: montos 0, no se crea movimiento en CF");
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
                        Fecha = fechaCierre,
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
                        Fecha = fechaCierre,
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
                    $"✅ Cierre recibido en CF | Efectivo: ${montoEfectivo} | Transferencias: ${montoTransferencia} | " +
                    $"Balance CF: Efectivo=${cajaFuerte.BalanceEfectivo} / Trans={cajaFuerte.BalanceTransferencias} / Total={cajaFuerte.BalanceTotal} | " +
                    $"Movimientos creados: {creados}");

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
                    return Ok(new { success = true, requiereCambio = config.RequiereCambioPassword, message = "Contraseña correcta" });

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
                if (HashPassword(request.PasswordActual) != config.PasswordHash)
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
        // BALANCES
        // =============================================

        /// <summary>
        /// GET /api/cajafuerte/balances
        /// Devuelve los balances acumulados actuales de la Caja Fuerte.
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
        /// GET /api/cajafuerte/movimientos-diarios?desde=&hasta=
        /// GET /api/cajafuerte/movimientos-diarios?cierreId=
        /// Devuelve los movimientos de Caja Diaria para ver el detalle dentro de un cierre.
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
                    query = query.Where(m => m.CierreCajaId == cierreId.Value);
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
                else
                {
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos diarios para Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos diarios" });
            }
        }

        // =============================================
        // MOVIMIENTOS DE CAJA FUERTE - LISTADO CON FILTROS
        // =============================================

        /// <summary>
        /// GET /api/cajafuerte/movimientos?vista=mensual|diario|semanal|rango
        /// GET /api/cajafuerte/movimientos?desde=2026-02-01&hasta=2026-02-28
        /// </summary>
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
                        cierreCajaId = m.CierreCajaId,
                        m.CreatedAt
                    }).ToList();

                var movimientosPorDia = movimientosFiltrados
                    .GroupBy(m => m.Fecha.ToLocalTime().Date)
                    .Select(g => new
                    {
                        fecha = g.Key.ToString("yyyy-MM-dd"),
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
                            cierreCajaId = m.CierreCajaId
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
                    balanceActual = new { efectivo = cajaFuerte.BalanceEfectivo, transferencias = cajaFuerte.BalanceTransferencias, total = cajaFuerte.BalanceTotal }
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
                    balanceActual = new { efectivo = cajaFuerte.BalanceEfectivo, transferencias = cajaFuerte.BalanceTransferencias, total = cajaFuerte.BalanceTotal }
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
                    balanceActual = new { efectivo = cajaFuerte.BalanceEfectivo, transferencias = cajaFuerte.BalanceTransferencias, total = cajaFuerte.BalanceTotal }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al transferir a Caja Diaria");
                return StatusCode(500, new { success = false, message = "Error al realizar transferencia" });
            }
        }

        // =============================================
        // RECIBIR DESDE CAJA (endpoint HTTP - respaldo manual)
        // =============================================

        [HttpPost("recibir-desde-caja")]
        public async Task<IActionResult> RecibirDesdeCaja([FromBody] RecibirDesdeCajaRequest request)
        {
            try
            {
                var ok = await RecibirDesdeCajaInternoAsync(
                    request.MontoEfectivo, request.MontoTransferencia,
                    request.FechaCierre, request.CierreCajaId);

                if (!ok)
                    return Ok(new { success = false, message = "Este cierre ya fue procesado anteriormente en Caja Fuerte o no hubo montos que transferir." });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                return Ok(new
                {
                    success = true,
                    message = $"Cierre recibido: Efectivo ${request.MontoEfectivo:N0}, Transferencias ${request.MontoTransferencia:N0}",
                    balanceActual = new { efectivo = cajaFuerte.BalanceEfectivo, transferencias = cajaFuerte.BalanceTransferencias, total = cajaFuerte.BalanceTotal }
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

                if (movimiento.Origen == "cierre_caja")
                    return BadRequest(new { success = false, message = "No se pueden eliminar movimientos de cierre de caja. Para corregir, realice un ajuste manual." });

                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                // Revertir el balance
                if (movimiento.Tipo == "ingreso")
                {
                    if (movimiento.MetodoPago == "efectivo")
                        cajaFuerte.BalanceEfectivo -= movimiento.Monto;
                    else
                        cajaFuerte.BalanceTransferencias -= movimiento.Monto;
                }
                else
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
                    balanceActual = new { efectivo = cajaFuerte.BalanceEfectivo, transferencias = cajaFuerte.BalanceTransferencias, total = cajaFuerte.BalanceTotal }
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

        /// <summary>
        /// GET /api/cajafuerte/consolidados
        /// Devuelve los consolidados guardados con su desglose completo.
        /// IMPORTANTE: Estos datos son una "foto" del momento en que se consolidó.
        /// Para ver datos actualizados usa /api/cajafuerte/consolidados/preview
        /// </summary>
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
                    efectivo = new
                    {
                        ingresos = c.TotalIngresosEfectivo,
                        egresos = c.TotalEgresosEfectivo,
                        balance = c.BalanceFinalEfectivo
                    },
                    transferencias = new
                    {
                        ingresos = c.TotalIngresosTransferencia,
                        egresos = c.TotalEgresosTransferencia,
                        balance = c.BalanceFinalTransferencia
                    },
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

        /// <summary>
        /// GET /api/cajafuerte/consolidados/preview?mes=2&anio=2026
        /// Calcula en tiempo real los totales de un mes a partir de los movimientos actuales,
        /// sin guardar nada. Útil para verificar antes de consolidar, o ver meses ya consolidados
        /// con datos frescos.
        /// </summary>
        [HttpGet("consolidados/preview")]
        public async Task<IActionResult> PreviewConsolidado(
            [FromQuery] int? mes = null,
            [FromQuery] int? anio = null)
        {
            try
            {
                var hoy = DateTime.Now;
                var mesConsulta = mes ?? hoy.Month;
                var anioConsulta = anio ?? hoy.Year;

                var primerDia = new DateTime(anioConsulta, mesConsulta, 1);
                var ultimoDia = primerDia.AddMonths(1);

                var todosMovimientos = await _context.MovimientosCajaFuerte.ToListAsync();
                var movimientos = todosMovimientos
                    .Where(m =>
                    {
                        var f = m.Fecha.ToLocalTime().Date;
                        return f >= primerDia && f < ultimoDia;
                    })
                    .ToList();

                var ingresosEfectivo = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var egresosEfectivo = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var ingresosTransferencia = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                var egresosTransferencia = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                // Desglose por origen
                var desglosePorOrigen = movimientos
                    .GroupBy(m => new { m.Origen, m.Tipo, m.MetodoPago })
                    .Select(g => new
                    {
                        origen = g.Key.Origen,
                        origenLabel = ObtenerOrigenLabel(g.Key.Origen),
                        tipo = g.Key.Tipo,
                        metodoPago = g.Key.MetodoPago,
                        total = g.Sum(m => m.Monto),
                        cantidad = g.Count()
                    })
                    .OrderBy(x => x.origen)
                    .ToList();

                var consolidadoExistente = await _context.ConsolidadosMensuales
                    .FirstOrDefaultAsync(c => c.Mes == mesConsulta && c.Anio == anioConsulta);

                return Ok(new
                {
                    success = true,
                    mes = mesConsulta,
                    anio = anioConsulta,
                    mesNombre = ObtenerNombreMes(mesConsulta),
                    periodo = $"{ObtenerNombreMes(mesConsulta)} {anioConsulta}",
                    yaConsolidado = consolidadoExistente != null,
                    consolidadoId = consolidadoExistente?.Id,
                    calculadoEnTiempoReal = true,
                    efectivo = new
                    {
                        ingresos = ingresosEfectivo,
                        egresos = egresosEfectivo,
                        balance = ingresosEfectivo - egresosEfectivo
                    },
                    transferencias = new
                    {
                        ingresos = ingresosTransferencia,
                        egresos = egresosTransferencia,
                        balance = ingresosTransferencia - egresosTransferencia
                    },
                    totales = new
                    {
                        ingresos = ingresosEfectivo + ingresosTransferencia,
                        egresos = egresosEfectivo + egresosTransferencia,
                        balance = (ingresosEfectivo - egresosEfectivo) + (ingresosTransferencia - egresosTransferencia)
                    },
                    cantidadMovimientos = movimientos.Count,
                    desglosePorOrigen
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al previsualizar consolidado");
                return StatusCode(500, new { success = false, message = "Error al previsualizar consolidado" });
            }
        }

        /// <summary>
        /// POST /api/cajafuerte/consolidar-mes
        /// Crea un consolidado mensual a partir de los movimientos reales en la DB.
        /// Solo puede consolidarse si el mes ya cerró (no el mes en curso).
        /// </summary>
        [HttpPost("consolidar-mes")]
        public async Task<IActionResult> ConsolidarMes([FromBody] ConsolidarMesRequest request)
        {
            try
            {
                var mes = request.Mes;
                var anio = request.Anio;

                // No consolidar el mes en curso
                var hoy = DateTime.Now;
                if (anio == hoy.Year && mes == hoy.Month)
                    return BadRequest(new { success = false, message = "No se puede consolidar el mes en curso. Espera a que cierre el mes." });

                var existeConsolidado = await _context.ConsolidadosMensuales
                    .AnyAsync(c => c.Mes == mes && c.Anio == anio);

                if (existeConsolidado)
                    return BadRequest(new { success = false, message = $"Ya existe un consolidado para {ObtenerNombreMes(mes)} {anio}. Usa PUT /recalcular-consolidado para actualizarlo." });

                var primerDia = new DateTime(anio, mes, 1);
                var ultimoDia = primerDia.AddMonths(1);

                var todosMovimientos = await _context.MovimientosCajaFuerte.ToListAsync();
                var movimientos = todosMovimientos
                    .Where(m =>
                    {
                        var f = m.Fecha.ToLocalTime().Date;
                        return f >= primerDia && f < ultimoDia;
                    })
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

                _logger.LogInformation($"Consolidado creado: {ObtenerNombreMes(mes)} {anio} | Efectivo: +{totalIngresosEfectivo} -{totalEgresosEfectivo} | Transferencias: +{totalIngresosTransferencia} -{totalEgresosTransferencia}");

                return Ok(new
                {
                    success = true,
                    message = $"{ObtenerNombreMes(mes)} {anio} consolidado correctamente ({movimientos.Count} movimientos)",
                    consolidado = new
                    {
                        consolidado.Id,
                        mes,
                        anio,
                        mesNombre = ObtenerNombreMes(mes),
                        movimientosProcesados = movimientos.Count,
                        efectivo = new { ingresos = totalIngresosEfectivo, egresos = totalEgresosEfectivo, balance = consolidado.BalanceFinalEfectivo },
                        transferencias = new { ingresos = totalIngresosTransferencia, egresos = totalEgresosTransferencia, balance = consolidado.BalanceFinalTransferencia },
                        totales = new
                        {
                            ingresos = totalIngresosEfectivo + totalIngresosTransferencia,
                            egresos = totalEgresosEfectivo + totalEgresosTransferencia,
                            balance = consolidado.BalanceFinalEfectivo + consolidado.BalanceFinalTransferencia
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

        /// <summary>
        /// PUT /api/cajafuerte/recalcular-consolidado
        /// Recalcula un consolidado existente a partir de los movimientos reales actuales.
        /// Útil cuando se eliminaron o corrigieron movimientos después de consolidar.
        /// </summary>
        [HttpPut("recalcular-consolidado")]
        public async Task<IActionResult> RecalcularConsolidado([FromBody] ConsolidarMesRequest request)
        {
            try
            {
                var mes = request.Mes;
                var anio = request.Anio;

                var consolidado = await _context.ConsolidadosMensuales
                    .FirstOrDefaultAsync(c => c.Mes == mes && c.Anio == anio);

                if (consolidado == null)
                    return NotFound(new { success = false, message = $"No existe consolidado para {ObtenerNombreMes(mes)} {anio}. Usa POST /consolidar-mes para crearlo." });

                var primerDia = new DateTime(anio, mes, 1);
                var ultimoDia = primerDia.AddMonths(1);

                var todosMovimientos = await _context.MovimientosCajaFuerte.ToListAsync();
                var movimientos = todosMovimientos
                    .Where(m =>
                    {
                        var f = m.Fecha.ToLocalTime().Date;
                        return f >= primerDia && f < ultimoDia;
                    })
                    .ToList();

                // Guardar valores anteriores para el log
                var anteriorEfectivoIngresos = consolidado.TotalIngresosEfectivo;
                var anteriorEfectivoEgresos = consolidado.TotalEgresosEfectivo;
                var anteriorTransfIngresos = consolidado.TotalIngresosTransferencia;
                var anteriorTransfEgresos = consolidado.TotalEgresosTransferencia;

                // Recalcular desde los movimientos reales
                consolidado.TotalIngresosEfectivo = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                consolidado.TotalEgresosEfectivo = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                consolidado.TotalIngresosTransferencia = movimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                consolidado.TotalEgresosTransferencia = movimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);
                consolidado.BalanceFinalEfectivo = consolidado.TotalIngresosEfectivo - consolidado.TotalEgresosEfectivo;
                consolidado.BalanceFinalTransferencia = consolidado.TotalIngresosTransferencia - consolidado.TotalEgresosTransferencia;
                consolidado.FechaConsolidacion = DateTime.Now; // Marca cuándo fue recalculado

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"Consolidado recalculado: {ObtenerNombreMes(mes)} {anio} | " +
                    $"Efectivo ingresos: {anteriorEfectivoIngresos} → {consolidado.TotalIngresosEfectivo} | " +
                    $"Efectivo egresos: {anteriorEfectivoEgresos} → {consolidado.TotalEgresosEfectivo}");

                return Ok(new
                {
                    success = true,
                    message = $"Consolidado de {ObtenerNombreMes(mes)} {anio} recalculado correctamente",
                    movimientosProcesados = movimientos.Count,
                    anterior = new
                    {
                        efectivo = new { ingresos = anteriorEfectivoIngresos, egresos = anteriorEfectivoEgresos, balance = anteriorEfectivoIngresos - anteriorEfectivoEgresos },
                        transferencias = new { ingresos = anteriorTransfIngresos, egresos = anteriorTransfEgresos, balance = anteriorTransfIngresos - anteriorTransfEgresos }
                    },
                    nuevo = new
                    {
                        efectivo = new { ingresos = consolidado.TotalIngresosEfectivo, egresos = consolidado.TotalEgresosEfectivo, balance = consolidado.BalanceFinalEfectivo },
                        transferencias = new { ingresos = consolidado.TotalIngresosTransferencia, egresos = consolidado.TotalEgresosTransferencia, balance = consolidado.BalanceFinalTransferencia },
                        totales = new
                        {
                            ingresos = consolidado.TotalIngresosEfectivo + consolidado.TotalIngresosTransferencia,
                            egresos = consolidado.TotalEgresosEfectivo + consolidado.TotalEgresosTransferencia,
                            balance = consolidado.BalanceFinalEfectivo + consolidado.BalanceFinalTransferencia
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recalcular consolidado");
                return StatusCode(500, new { success = false, message = "Error al recalcular consolidado" });
            }
        }

        // =============================================
        // DIAGNÓSTICO — solo para depuración
        // =============================================

        /// <summary>
        /// GET /api/cajafuerte/diagnostico?mes=2&anio=2026
        /// Muestra todos los movimientos CF del mes con sus datos crudos, el balance recalculado
        /// desde cero y lo compara con el balance guardado en la tabla CajasFuertes.
        /// Útil para detectar inconsistencias sin modificar nada.
        /// </summary>
        [HttpGet("diagnostico")]
        public async Task<IActionResult> Diagnostico(
            [FromQuery] int? mes = null,
            [FromQuery] int? anio = null)
        {
            try
            {
                var hoy = DateTime.Now;
                var mesConsulta = mes ?? hoy.Month;
                var anioConsulta = anio ?? hoy.Year;

                var primerDia = new DateTime(anioConsulta, mesConsulta, 1);
                var ultimoDia = primerDia.AddMonths(1);

                // Todos los movimientos CF del mes
                var todosMovimientos = await _context.MovimientosCajaFuerte.ToListAsync();
                var movsMes = todosMovimientos
                    .Where(m =>
                    {
                        var f = m.Fecha.ToLocalTime().Date;
                        return f >= primerDia && f < ultimoDia;
                    })
                    .OrderBy(m => m.Fecha)
                    .ToList();

                // Recalcular balance total desde TODOS los movimientos existentes
                var balanceRecalculadoEfectivo = todosMovimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto)
                                                  - todosMovimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);
                var balanceRecalculadoTransferencias = todosMovimientos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto)
                                                     - todosMovimientos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                var cajaFuerte = await _context.CajasFuertes.FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    periodo = $"{ObtenerNombreMes(mesConsulta)} {anioConsulta}",
                    movimientosDelMes = movsMes.Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Origen,
                        m.MetodoPago,
                        m.Monto,
                        m.Descripcion,
                        m.Categoria,
                        fechaUTC = m.Fecha,
                        fechaLocal = m.Fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        m.CierreCajaId,
                        m.CreatedAt
                    }),
                    totalesMes = new
                    {
                        ingresosEfectivo = movsMes.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        egresosEfectivo = movsMes.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto),
                        ingresosTransferencia = movsMes.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                        egresosTransferencia = movsMes.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto),
                    },
                    balanceGuardadoEnCajaFuerte = cajaFuerte == null ? null : new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    },
                    balanceRecalculadoDesdeMovimientos = new
                    {
                        efectivo = balanceRecalculadoEfectivo,
                        transferencias = balanceRecalculadoTransferencias,
                        total = balanceRecalculadoEfectivo + balanceRecalculadoTransferencias
                    },
                    inconsistenciaDetectada = cajaFuerte != null && (
                        cajaFuerte.BalanceEfectivo != balanceRecalculadoEfectivo ||
                        cajaFuerte.BalanceTransferencias != balanceRecalculadoTransferencias
                    ),
                    totalMovimientosEnSistema = todosMovimientos.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en diagnóstico");
                return StatusCode(500, new { success = false, message = "Error al ejecutar diagnóstico" });
            }
        }

        /// <summary>
        /// POST /api/cajafuerte/sincronizar-balance
        /// Recalcula el balance de CajaFuerte desde cero leyendo TODOS los movimientos.
        /// Usar solo si se detecta inconsistencia con /diagnostico.
        /// </summary>
        [HttpPost("sincronizar-balance")]
        public async Task<IActionResult> SincronizarBalance()
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var todos = await _context.MovimientosCajaFuerte.ToListAsync();

                var balanceAnterior = new { cajaFuerte.BalanceEfectivo, cajaFuerte.BalanceTransferencias, cajaFuerte.BalanceTotal };

                cajaFuerte.BalanceEfectivo = todos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto)
                                                 - todos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo").Sum(m => m.Monto);

                cajaFuerte.BalanceTransferencias = todos.Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto)
                                                 - todos.Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia").Sum(m => m.Monto);

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogWarning($"Balance CF sincronizado manualmente. Antes: Ef={balanceAnterior.BalanceEfectivo} Tr={balanceAnterior.BalanceTransferencias}. Ahora: Ef={cajaFuerte.BalanceEfectivo} Tr={cajaFuerte.BalanceTransferencias}");

                return Ok(new
                {
                    success = true,
                    message = "Balance sincronizado correctamente desde los movimientos reales",
                    movimientosProcesados = todos.Count,
                    anterior = balanceAnterior,
                    nuevo = new { cajaFuerte.BalanceEfectivo, cajaFuerte.BalanceTransferencias, cajaFuerte.BalanceTotal }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar balance");
                return StatusCode(500, new { success = false, message = "Error al sincronizar balance" });
            }
        }
    }

    // ===== REQUEST MODELS =====

    public class VerificarPasswordRequest { public string Password { get; set; } }
    public class CambiarPasswordRequest { public string PasswordActual { get; set; } public string PasswordNueva { get; set; } }
    public class ConsolidarMesRequest { public int Mes { get; set; } public int Anio { get; set; } }
    public class EliminarMovimientoRequest { public string? Motivo { get; set; } }

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
}