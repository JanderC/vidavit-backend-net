using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Services
{
    /// <summary>
    /// Servicio que ejecuta el cierre automático de caja a medianoche
    /// MODIFICADO: Ahora cierra solo movimientos pendientes y deja la caja en cero
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
                    _logger.LogInformation($"Fecha/Hora: {DateTime.UtcNow}");

                    // Obtener el último cierre para saber desde cuándo contar
                    var ultimoCierre = await context.CierresCaja
                        .OrderByDescending(c => c.FechaCierre)
                        .FirstOrDefaultAsync();

                    DateTime fechaDesde;
                    if (ultimoCierre != null)
                    {
                        fechaDesde = DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
                        _logger.LogInformation($"Último cierre: {fechaDesde}");
                    }
                    else
                    {
                        fechaDesde = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        _logger.LogInformation("No hay cierres previos - primer cierre del sistema");
                    }

                    // Obtener movimientos PENDIENTES (después del último cierre)
                    var movimientos = await context.MovimientosCaja
                        .Where(m => m.Fecha > fechaDesde)
                        .ToListAsync();

                    _logger.LogInformation($"Movimientos pendientes encontrados: {movimientos.Count}");

                    // Si no hay movimientos pendientes, no hacer cierre
                    if (movimientos.Count == 0)
                    {
                        _logger.LogInformation("No hay movimientos pendientes para cerrar");
                        _logger.LogInformation("========================================");
                        return;
                    }

                    // Calcular montos
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

                    // IMPORTANTE: La caja siempre empieza en 0 después de cada cierre
                    var efectivoInicial = 0;
                    var efectivoFinal = ingresosEfectivo - egresosEfectivo;
                    var balanceGeneral = totalIngresos - totalEgresos;

                    _logger.LogInformation("--- Resumen del cierre ---");
                    _logger.LogInformation($"Total ingresos: ${totalIngresos:F2}");
                    _logger.LogInformation($"Total egresos: ${totalEgresos:F2}");
                    _logger.LogInformation($"Balance general: ${balanceGeneral:F2}");
                    _logger.LogInformation($"Efectivo final: ${efectivoFinal:F2}");
                    _logger.LogInformation($"Transferencias netas: ${(ingresosTransferencia - egresosTransferencia):F2}");

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

                    // Crear registro de cierre
                    var cierre = new CierreCaja
                    {
                        Id = Guid.NewGuid(),
                        FechaCierre = DateTime.UtcNow,
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
                        CantidadMovimientos = movimientos.Count,
                        Observaciones = $"Cierre automático - Período: {fechaDesde:dd/MM/yyyy HH:mm} - {DateTime.UtcNow:dd/MM/yyyy HH:mm}",
                        UsuarioId = usuarioId,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.CierresCaja.Add(cierre);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("✅ Cierre automático completado exitosamente");
                    _logger.LogInformation($"ID del cierre: {cierre.Id}");
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

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo servicio de cierre automático de caja");
            await base.StopAsync(cancellationToken);
        }
    }
}