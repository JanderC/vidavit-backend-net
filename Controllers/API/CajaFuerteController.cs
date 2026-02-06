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
        /// Hash de contraseña usando SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Obtener o crear la instancia única de Caja Fuerte
        /// </summary>
        private async Task<CajaFuerte> ObtenerOCrearCajaFuerteAsync()
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

        /// <summary>
        /// Obtener o crear configuración de Caja Fuerte
        /// </summary>
        private async Task<ConfiguracionCajaFuerte> ObtenerOCrearConfiguracionAsync()
        {
            var config = await _context.ConfiguracionesCajaFuerte.FirstOrDefaultAsync();

            if (config == null)
            {
                // Contraseña inicial: 123456
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

        /// <summary>
        /// Convertir número de mes a nombre - MÉTODO ESTÁTICO
        /// </summary>
        private static string ObtenerNombreMes(int mes)
        {
            return mes switch
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
        }

        // =============================================
        // ENDPOINTS DE AUTENTICACIÓN
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

                return Ok(new
                {
                    success = false,
                    message = "Contraseña incorrecta"
                });
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
                {
                    return Ok(new { success = false, message = "Contraseña actual incorrecta" });
                }

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
        // DASHBOARD Y RESUMEN
        // =============================================

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();

                // Movimientos del mes actual
                var hoy = DateTime.Now.Date;
                var primerDiaMes = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1), DateTimeKind.Utc);

                var movimientosMes = await _context.MovimientosCajaFuerte
                    .Where(m => m.Fecha >= primerDiaMes)
                    .ToListAsync();

                var ingresosEfectivoMes = movimientosMes
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var egresosEfectivoMes = movimientosMes
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var ingresosTransferenciaMes = movimientosMes
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var egresosTransferenciaMes = movimientosMes
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    balances = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal,
                        ultimaActualizacion = cajaFuerte.UltimaActualizacion
                    },
                    movimientosMes = new
                    {
                        efectivo = new
                        {
                            ingresos = ingresosEfectivoMes,
                            egresos = egresosEfectivoMes,
                            balance = ingresosEfectivoMes - egresosEfectivoMes
                        },
                        transferencias = new
                        {
                            ingresos = ingresosTransferenciaMes,
                            egresos = egresosTransferenciaMes,
                            balance = ingresosTransferenciaMes - egresosTransferenciaMes
                        },
                        total = new
                        {
                            ingresos = ingresosEfectivoMes + ingresosTransferenciaMes,
                            egresos = egresosEfectivoMes + egresosTransferenciaMes,
                            balance = (ingresosEfectivoMes + ingresosTransferenciaMes) - (egresosEfectivoMes + egresosTransferenciaMes)
                        },
                        cantidad = movimientosMes.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener dashboard de Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener dashboard" });
            }
        }

        // =============================================
        // MOVIMIENTOS
        // =============================================

        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos([FromQuery] string vista = "mensual", [FromQuery] DateTime? fecha = null)
        {
            try
            {
                var fechaBase = fecha ?? DateTime.Now.Date;
                DateTime fechaDesde, fechaHasta;

                switch (vista.ToLower())
                {
                    case "diario":
                        fechaDesde = DateTime.SpecifyKind(fechaBase.Date, DateTimeKind.Utc);
                        fechaHasta = DateTime.SpecifyKind(fechaBase.Date.AddDays(1), DateTimeKind.Utc);
                        break;
                    case "semanal":
                        var diff = (7 + (fechaBase.DayOfWeek - DayOfWeek.Monday)) % 7;
                        fechaDesde = DateTime.SpecifyKind(fechaBase.AddDays(-diff).Date, DateTimeKind.Utc);
                        fechaHasta = DateTime.SpecifyKind(fechaDesde.AddDays(7), DateTimeKind.Utc);
                        break;
                    case "mensual":
                    default:
                        fechaDesde = DateTime.SpecifyKind(new DateTime(fechaBase.Year, fechaBase.Month, 1), DateTimeKind.Utc);
                        fechaHasta = DateTime.SpecifyKind(fechaDesde.AddMonths(1), DateTimeKind.Utc);
                        break;
                }

                var movimientos = await _context.MovimientosCajaFuerte
                    .Where(m => m.Fecha >= fechaDesde && m.Fecha < fechaHasta)
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.Tipo,
                        m.Origen,
                        m.MetodoPago,
                        m.Monto,
                        m.Descripcion,
                        m.Categoria,
                        m.Fecha,
                        m.CreatedAt
                    })
                    .ToListAsync();

                var totalIngresos = movimientos.Where(m => m.Tipo == "ingreso").Sum(m => m.Monto);
                var totalEgresos = movimientos.Where(m => m.Tipo == "egreso").Sum(m => m.Monto);

                return Ok(new
                {
                    success = true,
                    vista,
                    periodo = new
                    {
                        desde = fechaDesde,
                        hasta = fechaHasta
                    },
                    movimientos,
                    totales = new
                    {
                        ingresos = totalIngresos,
                        egresos = totalEgresos,
                        balance = totalIngresos - totalEgresos,
                        cantidad = movimientos.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos de Caja Fuerte");
                return StatusCode(500, new { success = false, message = "Error al obtener movimientos" });
            }
        }

        [HttpPost("registrar-egreso")]
        public async Task<IActionResult> RegistrarEgreso([FromBody] RegistrarEgresoRequest request)
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                if (request.MetodoPago == "efectivo" && cajaFuerte.BalanceEfectivo < request.Monto)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Saldo insuficiente en efectivo. Disponible: ${cajaFuerte.BalanceEfectivo:N2}"
                    });
                }

                if (request.MetodoPago == "transferencia" && cajaFuerte.BalanceTransferencias < request.Monto)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Saldo insuficiente en transferencias. Disponible: ${cajaFuerte.BalanceTransferencias:N2}"
                    });
                }

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
                {
                    cajaFuerte.BalanceEfectivo -= request.Monto;
                }
                else
                {
                    cajaFuerte.BalanceTransferencias -= request.Monto;
                }

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Egreso registrado en Caja Fuerte: ${request.Monto} ({request.MetodoPago})");

                return Ok(new
                {
                    success = true,
                    message = "Egreso registrado correctamente",
                    movimiento = new
                    {
                        movimiento.Id,
                        movimiento.Monto,
                        movimiento.MetodoPago,
                        movimiento.Descripcion,
                        movimiento.Categoria
                    },
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

        [HttpPost("transferir-a-caja")]
        public async Task<IActionResult> TransferirACaja([FromBody] TransferirACajaRequest request)
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                if (cajaFuerte.BalanceEfectivo < request.Monto)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Saldo insuficiente en efectivo. Disponible: ${cajaFuerte.BalanceEfectivo:N2}"
                    });
                }

                // 1. Crear movimiento de EGRESO en Caja Fuerte
                var movimiento = new MovimientoCajaFuerte
                {
                    Id = Guid.NewGuid(),
                    Tipo = "egreso",
                    Origen = "transferencia_a_caja",
                    MetodoPago = "efectivo",
                    Monto = request.Monto,
                    Descripcion = "Transferencia a Caja Diaria",
                    UsuarioId = usuarioId,
                    Fecha = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCajaFuerte.Add(movimiento);

                // 2. Actualizar balance de Caja Fuerte
                cajaFuerte.BalanceEfectivo -= request.Monto;
                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // 3. ⭐ CREAR MOVIMIENTO EN CAJA DIARIA DIRECTAMENTE EN LA BD
                var movimientoCaja = new MovimientoCaja
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

                _context.MovimientosCaja.Add(movimientoCaja);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Transferencia completada: ${request.Monto} - Caja Fuerte → Caja Diaria");

                return Ok(new
                {
                    success = true,
                    message = $"${request.Monto:N2} transferidos a Caja Diaria",
                    balanceActual = new
                    {
                        efectivo = cajaFuerte.BalanceEfectivo,
                        transferencias = cajaFuerte.BalanceTransferencias,
                        total = cajaFuerte.BalanceTotal
                    },
                    movimientoCajaCreado = new
                    {
                        id = movimientoCaja.Id,
                        monto = movimientoCaja.Monto,
                        descripcion = movimientoCaja.Descripcion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al transferir a Caja Diaria");
                return StatusCode(500, new { success = false, message = "Error al realizar transferencia", error = ex.Message });
            }
        }
        [HttpPost("recibir-desde-caja")]
        public async Task<IActionResult> RecibirDesdeCaja([FromBody] RecibirDesdeCajaRequest request)
        {
            try
            {
                var cajaFuerte = await ObtenerOCrearCajaFuerteAsync();
                var usuarioId = await ObtenerUsuarioSistemaAsync();

                if (request.MontoEfectivo > 0)
                {
                    var movimientoEfectivo = new MovimientoCajaFuerte
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Origen = "cierre_caja",
                        MetodoPago = "efectivo",
                        Monto = request.MontoEfectivo,
                        Descripcion = $"Cierre de Caja - {request.FechaCierre:dd/MM/yyyy}",
                        CierreCajaId = request.CierreCajaId,
                        UsuarioId = usuarioId,
                        Fecha = DateTime.Now,
                        CreatedAt = DateTime.Now
                    };
                    _context.MovimientosCajaFuerte.Add(movimientoEfectivo);
                    cajaFuerte.BalanceEfectivo += request.MontoEfectivo;
                }

                if (request.MontoTransferencia > 0)
                {
                    var movimientoTransferencia = new MovimientoCajaFuerte
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Origen = "cierre_caja",
                        MetodoPago = "transferencia",
                        Monto = request.MontoTransferencia,
                        Descripcion = $"Cierre de Caja - {request.FechaCierre:dd/MM/yyyy}",
                        CierreCajaId = request.CierreCajaId,
                        UsuarioId = usuarioId,
                        Fecha = DateTime.Now,
                        CreatedAt = DateTime.Now
                    };
                    _context.MovimientosCajaFuerte.Add(movimientoTransferencia);
                    cajaFuerte.BalanceTransferencias += request.MontoTransferencia;
                }

                cajaFuerte.BalanceTotal = cajaFuerte.BalanceEfectivo + cajaFuerte.BalanceTransferencias;
                cajaFuerte.UltimaActualizacion = DateTime.Now;
                cajaFuerte.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Recibido de Caja Diaria - Efectivo: ${request.MontoEfectivo}, Transferencia: ${request.MontoTransferencia}");

                return Ok(new
                {
                    success = true,
                    message = "Dinero recibido correctamente",
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
        // CONSOLIDADOS HISTÓRICOS
        // =============================================

        [HttpGet("consolidados")]
        public async Task<IActionResult> GetConsolidados()
        {
            try
            {
                // Obtener consolidados primero
                var consolidadosDB = await _context.ConsolidadosMensuales
                    .OrderByDescending(c => c.Anio)
                    .ThenByDescending(c => c.Mes)
                    .ToListAsync();

                // Proyectar en memoria (no en SQL)
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

                return Ok(new
                {
                    success = true,
                    consolidados,
                    total = consolidados.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consolidados");
                return StatusCode(500, new { success = false, message = "Error al obtener consolidados" });
            }
        }

        [HttpGet("consolidados/{id}")]
        public async Task<IActionResult> GetConsolidadoDetalle(Guid id)
        {
            try
            {
                var consolidado = await _context.ConsolidadosMensuales.FindAsync(id);

                if (consolidado == null)
                {
                    return NotFound(new { success = false, message = "Consolidado no encontrado" });
                }

                return Ok(new
                {
                    success = true,
                    consolidado = new
                    {
                        consolidado.Id,
                        consolidado.Mes,
                        consolidado.Anio,
                        mesNombre = ObtenerNombreMes(consolidado.Mes),
                        periodo = $"{ObtenerNombreMes(consolidado.Mes)} {consolidado.Anio}",
                        efectivo = new
                        {
                            ingresos = consolidado.TotalIngresosEfectivo,
                            egresos = consolidado.TotalEgresosEfectivo,
                            balance = consolidado.BalanceFinalEfectivo
                        },
                        transferencias = new
                        {
                            ingresos = consolidado.TotalIngresosTransferencia,
                            egresos = consolidado.TotalEgresosTransferencia,
                            balance = consolidado.BalanceFinalTransferencia
                        },
                        totales = new
                        {
                            ingresos = consolidado.TotalIngresosEfectivo + consolidado.TotalIngresosTransferencia,
                            egresos = consolidado.TotalEgresosEfectivo + consolidado.TotalEgresosTransferencia,
                            balance = consolidado.BalanceFinalEfectivo + consolidado.BalanceFinalTransferencia
                        },
                        consolidado.FechaConsolidacion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de consolidado");
                return StatusCode(500, new { success = false, message = "Error al obtener detalle" });
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
                {
                    return BadRequest(new { success = false, message = "Ya existe un consolidado para este mes" });
                }

                var primerDia = DateTime.SpecifyKind(new DateTime(anio, mes, 1), DateTimeKind.Utc);
                var ultimoDia = DateTime.SpecifyKind(primerDia.AddMonths(1), DateTimeKind.Utc);

                var movimientos = await _context.MovimientosCajaFuerte
                    .Where(m => m.Fecha >= primerDia && m.Fecha < ultimoDia)
                    .ToListAsync();

                var totalIngresosEfectivo = movimientos
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var totalEgresosEfectivo = movimientos
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "efectivo")
                    .Sum(m => m.Monto);

                var totalIngresosTransferencia = movimientos
                    .Where(m => m.Tipo == "ingreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

                var totalEgresosTransferencia = movimientos
                    .Where(m => m.Tipo == "egreso" && m.MetodoPago == "transferencia")
                    .Sum(m => m.Monto);

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
                _context.MovimientosCajaFuerte.RemoveRange(movimientos);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Mes consolidado: {mes}/{anio} - {movimientos.Count} movimientos procesados");

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

    public class VerificarPasswordRequest
    {
        public string Password { get; set; }
    }

    public class CambiarPasswordRequest
    {
        public string PasswordActual { get; set; }
        public string PasswordNueva { get; set; }
    }

    public class RegistrarEgresoRequest
    {
        public string MetodoPago { get; set; }
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public string Categoria { get; set; }
    }

    public class TransferirACajaRequest
    {
        public decimal Monto { get; set; }
    }

    public class RecibirDesdeCajaRequest
    {
        public decimal MontoEfectivo { get; set; }
        public decimal MontoTransferencia { get; set; }
        public DateTime FechaCierre { get; set; }
        public Guid CierreCajaId { get; set; }
    }

    public class ConsolidarMesRequest
    {
        public int Mes { get; set; }
        public int Anio { get; set; }
    }
}