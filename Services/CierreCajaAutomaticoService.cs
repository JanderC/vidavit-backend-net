using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Services
{
    /// <summary>
    /// Servicio que ejecuta el cierre automático de caja a medianoche
    /// MEJORADO: Cierra TODOS los días pendientes hasta ayer, no solo el día inmediato anterior
    /// </summary>
    public class CierreCajaAutomaticoService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CierreCajaAutomaticoService> _logger;

        public CierreCajaAutomaticoService(
            IServiceProvider serviceProvider,
            ILogger<CierreCajaAutomaticoService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de cierre automático de caja iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                var ahora = DateTime.Now;
                var proximaMedianoche = ahora.Date.AddDays(1);
                var tiempoHastaMedianoche = proximaMedianoche - ahora;

                _logger.LogInformation($"Próximo cierre automático en: {tiempoHastaMedianoche.TotalHours:F2} horas ({proximaMedianoche})");

                try
                {
                    // Esperar hasta medianoche
                    await Task.Delay(tiempoHastaMedianoche, stoppingToken);

                    // Ejecutar cierre automático
                    await EjecutarCierreAutomatico();
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Servicio de cierre automático detenido");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el servicio de cierre automático");
                    // Esperar 1 hora antes de reintentar en caso de error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task EjecutarCierreAutomatico()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    _logger.LogInformation("========================================");
                    _logger.LogInformation("Iniciando cierre automático de caja...");
                    _logger.LogInformation($"Fecha/Hora: {DateTime.Now}");

                    var hoy = DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Utc);

                    // Obtener TODOS los días con movimientos pendientes (no cerrados) HASTA AYER
                    var diasPendientes = await context.MovimientosCaja
                        .Where(m => !m.Cerrado && m.Fecha < hoy)
                        .Select(m => m.Fecha.Date)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToListAsync();

                    if (diasPendientes.Count == 0)
                    {
                        _logger.LogInformation("No hay días pendientes para cerrar");
                        _logger.LogInformation("========================================");
                        return;
                    }

                    _logger.LogInformation($"Días pendientes encontrados: {diasPendientes.Count}");
                    foreach (var dia in diasPendientes)
                    {
                        _logger.LogInformation($"  - {dia:dd/MM/yyyy}");
                    }

                    // Obtener el primer usuario activo del sistema
                    var usuarioId = await context.Usuarios
                        .Where(u => u.Activo)
                        .OrderBy(u => u.CreatedAt)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync();

                    if (usuarioId == Guid.Empty)
                    {
                        _logger.LogError("No se encontró ningún usuario activo para el cierre automático");
                        return;
                    }

                    // CERRAR CADA DÍA PENDIENTE
                    int cierresRealizados = 0;
                    foreach (var diaPendiente in diasPendientes)
                    {
                        try
                        {
                            await CerrarDia(context, diaPendiente, usuarioId);
                            cierresRealizados++;
                            _logger.LogInformation($"✅ Día {diaPendiente:dd/MM/yyyy} cerrado correctamente");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"❌ Error al cerrar día {diaPendiente:dd/MM/yyyy}");
                        }
                    }

                    _logger.LogInformation($"Resumen: {cierresRealizados} de {diasPendientes.Count} días cerrados");
                    _logger.LogInformation("========================================");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al ejecutar cierre automático de caja");
                _logger.LogError($"Mensaje: {ex.Message}");
                _logger.LogError($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Cierra un día específico con sus movimientos pendientes
        /// </summary>
        private async Task CerrarDia(AppDbContext context, DateTime fecha, Guid usuarioId)
        {
            var inicioDia = fecha.Date;
            var finDia = inicioDia.AddDays(1);

            _logger.LogInformation($"--- Cerrando día: {fecha:dd/MM/yyyy} ---");

            // Obtener movimientos NO CERRADOS de este día específico
            var movimientosPendientes = await context.MovimientosCaja
                .Where(m => !m.Cerrado && m.Fecha >= inicioDia && m.Fecha < finDia)
                .ToListAsync();

            if (movimientosPendientes.Count == 0)
            {
                _logger.LogWarning($"No hay movimientos pendientes para {fecha:dd/MM/yyyy}");
                return;
            }

            // Calcular montos
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

            _logger.LogInformation($"  Movimientos: {movimientosPendientes.Count}");
            _logger.LogInformation($"  Ingresos: ${totalIngresos:F2}");
            _logger.LogInformation($"  Egresos: ${totalEgresos:F2}");
            _logger.LogInformation($"  Balance: ${balanceGeneral:F2}");

            // Crear registro de cierre
            var cierre = new CierreCaja
            {
                Id = Guid.NewGuid(),
                FechaCierre = DateTime.Now,
                TipoCierre = "automatico",
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
                Observaciones = $"Cierre automático - Día: {fecha:dd/MM/yyyy}",
                UsuarioId = usuarioId,
                CreatedAt = DateTime.Now
            };

            context.CierresCaja.Add(cierre);
            await context.SaveChangesAsync();

            // MARCAR TODOS LOS MOVIMIENTOS COMO CERRADOS
            foreach (var movimiento in movimientosPendientes)
            {
                movimiento.Cerrado = true;
                movimiento.CierreCajaId = cierre.Id;
            }

            await context.SaveChangesAsync();

            _logger.LogInformation($"  ID del cierre: {cierre.Id}");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo servicio de cierre automático de caja");
            await base.StopAsync(cancellationToken);
        }
    }
}