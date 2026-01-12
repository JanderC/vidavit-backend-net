using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Services
{
    /// <summary>
    /// Servicio que ejecuta el cierre automático de caja a medianoche
    /// </summary>
    public class CierreCajaAutomaticoService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CierreCajaAutomaticoService> _logger;
        private Timer? _timer;

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

                _logger.LogInformation($"Próximo cierre automático en: {tiempoHastaMedianoche.TotalHours:F2} horas");

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

                    _logger.LogInformation("Iniciando cierre automático de caja...");

                    // Obtener el último cierre
                    var ultimoCierre = await context.CierresCaja
                        .OrderByDescending(c => c.FechaCierre)
                        .FirstOrDefaultAsync();

                    decimal saldoAnterior = 0; // Siempre empezar desde 0

                    // Obtener movimientos DESPUÉS del último cierre
                    DateTime fechaDesde;
                    if (ultimoCierre != null)
                    {
                        fechaDesde = DateTime.SpecifyKind(ultimoCierre.FechaCierre, DateTimeKind.Utc);
                    }
                    else
                    {
                        fechaDesde = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    }

                    var movimientos = await context.MovimientosCaja
                        .Where(m => m.Fecha > fechaDesde)
                        .ToListAsync();

                    // Si no hay movimientos, no hacer cierre
                    if (movimientos.Count == 0)
                    {
                        _logger.LogInformation("No hay movimientos para cerrar hoy");
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

                    var efectivoFinal = saldoAnterior + ingresosEfectivo - egresosEfectivo;
                    var balanceGeneral = totalIngresos - totalEgresos;

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
                        EfectivoInicial = saldoAnterior,
                        IngresosEfectivo = ingresosEfectivo,
                        EgresosEfectivo = egresosEfectivo,
                        EfectivoFinal = efectivoFinal,
                        IngresosTransferencia = ingresosTransferencia,
                        EgresosTransferencia = egresosTransferencia,
                        TotalIngresos = totalIngresos,
                        TotalEgresos = totalEgresos,
                        BalanceGeneral = balanceGeneral,
                        CantidadMovimientos = movimientos.Count,
                        Observaciones = "Cierre automático generado por el sistema",
                        UsuarioId = usuarioId,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.CierresCaja.Add(cierre);
                    await context.SaveChangesAsync();

                    _logger.LogInformation($"Cierre automático completado. Efectivo final: ${efectivoFinal:F2}, Balance: ${balanceGeneral:F2}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar cierre automático de caja");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo servicio de cierre automático de caja");
            _timer?.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}