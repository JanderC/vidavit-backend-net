using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using VidaFit.Data;
using VidaFitBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VidaFit.Services
{
    /// <summary>
    /// Servicio que se ejecuta automáticamente el primer día de cada mes
    /// para consolidar los movimientos del mes anterior
    /// </summary>
    public class ConsolidacionMensualService : BackgroundService
    {
        private readonly ILogger<ConsolidacionMensualService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private DateTime _ultimaEjecucion = DateTime.MinValue;

        public ConsolidacionMensualService(
            ILogger<ConsolidacionMensualService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de Consolidación Mensual iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ahora = DateTime.Now;

                    // Verificar si es el primer día del mes y no se ha ejecutado hoy
                    if (ahora.Day == 1 && _ultimaEjecucion.Date != ahora.Date)
                    {
                        _logger.LogInformation($"🗓️ Iniciando consolidación automática - {ahora:yyyy-MM-dd HH:mm:ss}");

                        // Obtener mes y año anterior
                        var mesAnterior = ahora.AddMonths(-1);
                        var mes = mesAnterior.Month;
                        var anio = mesAnterior.Year;

                        await ConsolidarMes(mes, anio);

                        _ultimaEjecucion = ahora;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error en servicio de consolidación mensual");
                }

                // Verificar cada hora
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Servicio de Consolidación Mensual detenido");
        }

        private async Task ConsolidarMes(int mes, int anio)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // Verificar que no exista ya un consolidado para este mes
                var existeConsolidado = await context.ConsolidadosMensuales
                    .AnyAsync(c => c.Mes == mes && c.Anio == anio);

                if (existeConsolidado)
                {
                    _logger.LogInformation($"⚠️ Ya existe consolidado para {mes}/{anio}");
                    return;
                }

                // ===== FIX: DateTime con UTC explícito =====
                var primerDia = DateTime.SpecifyKind(new DateTime(anio, mes, 1), DateTimeKind.Utc);
                var ultimoDia = DateTime.SpecifyKind(primerDia.AddMonths(1), DateTimeKind.Utc);

                var movimientos = await context.MovimientosCajaFuerte
                    .Where(m => m.Fecha >= primerDia && m.Fecha < ultimoDia)
                    .ToListAsync();

                if (movimientos.Count == 0)
                {
                    _logger.LogInformation($"ℹ️ No hay movimientos para consolidar en {mes}/{anio}");
                    return;
                }

                // Calcular totales
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

                // Crear consolidado
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

                context.ConsolidadosMensuales.Add(consolidado);

                // ELIMINAR los movimientos ya consolidados
                context.MovimientosCajaFuerte.RemoveRange(movimientos);

                await context.SaveChangesAsync();

                _logger.LogInformation($"✅ Consolidado creado: {ObtenerNombreMes(mes)} {anio} - {movimientos.Count} movimientos procesados");
                _logger.LogInformation($"   💰 Balance Final: Efectivo ${consolidado.BalanceFinalEfectivo:N2}, Transferencias ${consolidado.BalanceFinalTransferencia:N2}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error al consolidar mes {mes}/{anio}");
            }
        }

        private string ObtenerNombreMes(int mes)
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
    }
}