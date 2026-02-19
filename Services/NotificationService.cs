using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Services
{
    // ==================== INTERFACE ====================
    public interface INotificationService
    {
        Task CrearNotificacion(string tipo, string titulo, string mensaje, Guid? clienteId = null, string prioridad = "normal");
        Task<int> VerificarMembresiasVencidas();
        Task<int> GetNotificacionesNoLeidas();
        Task<List<Notificacion>> GetNotificacionesRecientes(int limit = 10);
        Task MarcarComoLeida(Guid notificacionId);
        Task MarcarTodasComoLeidas();
        Task<Dictionary<string, int>> GetEstadisticasNotificaciones();
    }

    // ==================== IMPLEMENTATION ====================
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Crea una nueva notificación en el sistema
        /// </summary>
        /// <param name="tipo">Tipo de notificación (vencimiento_proximo, membresia_vencida, etc.)</param>
        /// <param name="titulo">Título de la notificación</param>
        /// <param name="mensaje">Mensaje descriptivo</param>
        /// <param name="clienteId">ID del cliente relacionado (opcional)</param>
        /// <param name="prioridad">Nivel de prioridad (baja, normal, alta, urgente)</param>
        /// <returns>Task completada</returns>
        public async Task CrearNotificacion(
            string tipo,
            string titulo,
            string mensaje,
            Guid? clienteId = null,
            string prioridad = "normal")
        {
            try
            {
                var notificacion = new Notificacion
                {
                    Tipo = tipo,
                    Titulo = titulo,
                    Mensaje = mensaje,
                    ClienteId = clienteId,
                    Prioridad = prioridad,
                    Leida = false,
                    FechaCreacion = DateTime.Now
                };

                _context.Notificaciones.Add(notificacion);
                await _context.SaveChangesAsync();

                // Log para debugging
                Console.WriteLine($"✓ Notificación creada: [{prioridad.ToUpper()}] {titulo}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error creando notificación: {ex.Message}");
                // No lanzar excepción para no interrumpir el flujo principal
            }
        }

        /// <summary>
        /// Verifica y actualiza el estado de membresías vencidas
        /// Crea notificaciones automáticas para membresías próximas a vencer o vencidas
        /// </summary>
        /// <returns>Número de membresías actualizadas</returns>
        public async Task<int> VerificarMembresiasVencidas()
        {
            try
            {
                var hoy = DateTime.Now.Date;
                var contador = 0;

                Console.WriteLine($"Verificando membresías vencidas para la fecha: {hoy:dd/MM/yyyy}");

                // ===== PASO 1: Marcar membresías vencidas =====
                var membresiasVencidas = await _context.Membresias
                    .Include(m => m.Cliente)
                    .Where(m => m.Estado == "activa" && m.FechaVencimiento < hoy)
                    .ToListAsync();

                foreach (var membresia in membresiasVencidas)
                {
                    membresia.Estado = "vencida";
                    membresia.UpdatedAt = DateTime.Now;

                    // Crear notificación de membresía vencida
                    await CrearNotificacion(
                        tipo: "membresia_vencida",
                        titulo: "Mensualidad Vencida",
                        mensaje: $"La membresía de {membresia.Cliente.Nombre} {membresia.Cliente.Apellido} ha vencido el {membresia.FechaVencimiento:dd/MM/yyyy}",
                        clienteId: membresia.ClienteId,
                        prioridad: "alta"
                    );

                    contador++;
                }

                if (membresiasVencidas.Any())
                {
                    Console.WriteLine($"✓ {contador} membresía(s) marcada(s) como vencida(s)");
                }

                // ===== PASO 2: Notificar membresías próximas a vencer (7 días) =====
                var fechaLimite7Dias = hoy.AddDays(7);
                var membresiasProximas = await _context.Membresias
                    .Include(m => m.Cliente)
                    .Where(m => m.Estado == "activa"
                               && m.FechaVencimiento >= hoy
                               && m.FechaVencimiento <= fechaLimite7Dias)
                    .ToListAsync();

                foreach (var membresia in membresiasProximas)
                {
                    var diasRestantes = (membresia.FechaVencimiento - hoy).Days;

                    // Verificar si ya existe notificación para hoy
                    var existeNotificacion = await _context.Notificaciones
                        .AnyAsync(n => n.ClienteId == membresia.ClienteId
                                      && n.Tipo == "vencimiento_proximo"
                                      && n.FechaCreacion.Date == hoy);

                    if (!existeNotificacion)
                    {
                        var prioridad = diasRestantes <= 3 ? "alta" : "normal";
                        var emoji = diasRestantes <= 3 ? "🔴" : "🟡";

                        await CrearNotificacion(
                            tipo: "vencimiento_proximo",
                            titulo: $"{emoji} Membresía por Vencer",
                            mensaje: $"La membresía de {membresia.Cliente.Nombre} {membresia.Cliente.Apellido} vence en {diasRestantes} día{(diasRestantes != 1 ? "s" : "")}",
                            clienteId: membresia.ClienteId,
                            prioridad: prioridad
                        );
                    }
                }

                if (membresiasProximas.Any())
                {
                    Console.WriteLine($"✓ {membresiasProximas.Count} notificación(es) de vencimiento próximo creada(s)");
                }

                // ===== PASO 3: Verificar stock bajo de productos =====
                var productosStockBajo = await _context.Productos
                    .Where(p => p.Activo && p.Stock <= 10)
                    .ToListAsync();

                foreach (var producto in productosStockBajo)
                {
                    // Verificar si ya existe notificación de stock bajo hoy
                    var existeNotificacion = await _context.Notificaciones
                        .AnyAsync(n => n.Tipo == "stock_bajo"
                                      && n.Mensaje.Contains(producto.Nombre)
                                      && n.FechaCreacion.Date == hoy);

                    if (!existeNotificacion)
                    {
                        var prioridad = producto.Stock <= 5 ? "alta" : "normal";

                        await CrearNotificacion(
                            tipo: "stock_bajo",
                            titulo: "⚠️ Stock Bajo",
                            mensaje: $"El producto '{producto.Nombre}' tiene solo {producto.Stock} unidades disponibles",
                            clienteId: null,
                            prioridad: prioridad
                        );
                    }
                }

                if (productosStockBajo.Any())
                {
                    Console.WriteLine($"✓ {productosStockBajo.Count} alerta(s) de stock bajo verificada(s)");
                }

                // Guardar todos los cambios
                await _context.SaveChangesAsync();

                Console.WriteLine($"✓ Verificación de membresías completada. Total actualizaciones: {contador}");
                return contador;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error verificando membresías: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Obtiene el conteo de notificaciones no leídas
        /// </summary>
        /// <returns>Número de notificaciones sin leer</returns>
        public async Task<int> GetNotificacionesNoLeidas()
        {
            try
            {
                return await _context.Notificaciones
                    .CountAsync(n => !n.Leida);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error obteniendo notificaciones no leídas: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Obtiene las notificaciones más recientes
        /// </summary>
        /// <param name="limit">Cantidad máxima de notificaciones</param>
        /// <returns>Lista de notificaciones ordenadas por fecha</returns>
        public async Task<List<Notificacion>> GetNotificacionesRecientes(int limit = 10)
        {
            try
            {
                return await _context.Notificaciones
                    .Include(n => n.Cliente)
                    .OrderByDescending(n => n.FechaCreacion)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error obteniendo notificaciones recientes: {ex.Message}");
                return new List<Notificacion>();
            }
        }

        /// <summary>
        /// Marca una notificación como leída
        /// </summary>
        /// <param name="notificacionId">ID de la notificación</param>
        /// <returns>Task completada</returns>
        public async Task MarcarComoLeida(Guid notificacionId)
        {
            try
            {
                var notificacion = await _context.Notificaciones.FindAsync(notificacionId);

                if (notificacion != null && !notificacion.Leida)
                {
                    notificacion.Leida = true;
                    notificacion.FechaLeida = DateTime.Now;
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✓ Notificación {notificacionId} marcada como leída");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error marcando notificación como leída: {ex.Message}");
            }
        }

        /// <summary>
        /// Marca todas las notificaciones como leídas
        /// </summary>
        /// <returns>Task completada</returns>
        public async Task MarcarTodasComoLeidas()
        {
            try
            {
                var notificacionesNoLeidas = await _context.Notificaciones
                    .Where(n => !n.Leida)
                    .ToListAsync();

                foreach (var notificacion in notificacionesNoLeidas)
                {
                    notificacion.Leida = true;
                    notificacion.FechaLeida = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"✓ {notificacionesNoLeidas.Count} notificación(es) marcada(s) como leída(s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error marcando todas las notificaciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de notificaciones por tipo y prioridad
        /// </summary>
        /// <returns>Diccionario con conteos por categoría</returns>
        public async Task<Dictionary<string, int>> GetEstadisticasNotificaciones()
        {
            try
            {
                var stats = new Dictionary<string, int>();
                var hoy = DateTime.Now.Date;
                var hace7Dias = hoy.AddDays(-7);

                // Total de notificaciones
                stats["total"] = await _context.Notificaciones.CountAsync();

                // No leídas
                stats["noLeidas"] = await _context.Notificaciones
                    .CountAsync(n => !n.Leida);

                // Por tipo
                stats["vencimientoProximo"] = await _context.Notificaciones
                    .CountAsync(n => n.Tipo == "vencimiento_proximo" && !n.Leida);

                stats["membresiaVencida"] = await _context.Notificaciones
                    .CountAsync(n => n.Tipo == "membresia_vencida" && !n.Leida);

                stats["stockBajo"] = await _context.Notificaciones
                    .CountAsync(n => n.Tipo == "stock_bajo" && !n.Leida);

                stats["pagoPendiente"] = await _context.Notificaciones
                    .CountAsync(n => n.Tipo == "pago_pendiente" && !n.Leida);

                // Por prioridad
                stats["prioridadAlta"] = await _context.Notificaciones
                    .CountAsync(n => n.Prioridad == "alta" && !n.Leida);

                stats["prioridadUrgente"] = await _context.Notificaciones
                    .CountAsync(n => n.Prioridad == "urgente" && !n.Leida);

                // De la última semana
                stats["ultimaSemana"] = await _context.Notificaciones
                    .CountAsync(n => n.FechaCreacion >= hace7Dias);

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error obteniendo estadísticas de notificaciones: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Limpia notificaciones antiguas (más de 30 días y leídas)
        /// </summary>
        /// <returns>Número de notificaciones eliminadas</returns>
        public async Task<int> LimpiarNotificacionesAntiguas()
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(-30);

                var notificacionesAntiguas = await _context.Notificaciones
                    .Where(n => n.Leida && n.FechaCreacion < fechaLimite)
                    .ToListAsync();

                _context.Notificaciones.RemoveRange(notificacionesAntiguas);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✓ {notificacionesAntiguas.Count} notificación(es) antigua(s) eliminada(s)");
                return notificacionesAntiguas.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error limpiando notificaciones antiguas: {ex.Message}");
                return 0;
            }
        }
    }
}