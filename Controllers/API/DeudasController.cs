using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeudasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeudasController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtener todas las deudas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDeudas([FromQuery] string estado = null)
        {
            try
            {
                var query = _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .Include(d => d.Abonos)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(estado))
                {
                    query = query.Where(d => d.Estado == estado);
                }

                var deudas = await query
                    .OrderByDescending(d => d.FechaCreacion)
                    .Select(d => new
                    {
                        d.Id,
                        d.ClienteId,
                        clienteNombre = $"{d.Cliente.Nombre} {d.Cliente.Apellido}",
                        clienteCedula = d.Cliente.Cedula,
                        d.Concepto,
                        d.MontoTotal,
                        d.MontoPagado,
                        d.Saldo,
                        d.Estado,
                        d.FechaCreacion,
                        d.FechaVencimiento,
                        d.Notas,
                        cantidadAbonos = d.Abonos.Count
                    })
                    .ToListAsync();

                var totalDeudas = deudas.Sum(d => d.MontoTotal);
                var totalSaldoPendiente = deudas.Where(d => d.Estado != "pagada").Sum(d => d.Saldo);

                return Ok(new
                {
                    success = true,
                    totalDeudas,
                    totalSaldoPendiente,
                    cantidadDeudas = deudas.Count,
                    data = deudas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener deudas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener deudas de un cliente específico
        /// </summary>
        [HttpGet("cliente/{clienteId}")]
        public async Task<IActionResult> GetDeudasCliente(Guid clienteId)
        {
            try
            {
                var deudas = await _context.DeudasClientes
                    .Include(d => d.Abonos)
                    .Where(d => d.ClienteId == clienteId)
                    .OrderByDescending(d => d.FechaCreacion)
                    .Select(d => new
                    {
                        d.Id,
                        d.Concepto,
                        d.MontoTotal,
                        d.MontoPagado,
                        d.Saldo,
                        d.Estado,
                        d.FechaCreacion,
                        d.FechaVencimiento,
                        d.Notas,
                        abonos = d.Abonos.Select(a => new
                        {
                            a.Id,
                            a.Monto,
                            a.FechaAbono,
                            a.MetodoPago,
                            a.Notas
                        }).OrderByDescending(a => a.FechaAbono).ToList()
                    })
                    .ToListAsync();

                var totalAdeudado = deudas.Where(d => d.Estado != "pagada").Sum(d => d.Saldo);

                return Ok(new
                {
                    success = true,
                    totalAdeudado,
                    cantidadDeudas = deudas.Count,
                    data = deudas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener deudas del cliente",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Crear una nueva deuda
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateDeuda([FromBody] JsonElement data)
        {
            try
            {
                var clienteId = Guid.Parse(data.GetProperty("clienteId").GetString());
                var concepto = data.GetProperty("concepto").GetString();
                var montoTotal = data.GetProperty("montoTotal").GetDecimal();
                var notas = data.TryGetProperty("notas", out var notasEl) ? notasEl.GetString() : null;

                DateTime? fechaVencimiento = null;
                if (data.TryGetProperty("fechaVencimiento", out var fechaEl) && !string.IsNullOrWhiteSpace(fechaEl.GetString()))
                {
                    fechaVencimiento = DateTime.SpecifyKind(DateTime.Parse(fechaEl.GetString()), DateTimeKind.Utc);
                }

                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null)
                {
                    return Ok(new { success = false, message = "Cliente no encontrado" });
                }

                var deuda = new DeudaCliente
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    Concepto = concepto,
                    MontoTotal = montoTotal,
                    MontoPagado = 0,
                    Saldo = montoTotal,
                    Estado = "pendiente",
                    FechaCreacion = DateTime.UtcNow,
                    FechaVencimiento = fechaVencimiento,
                    Notas = notas,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DeudasClientes.Add(deuda);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Deuda creada correctamente",
                    data = deuda
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al crear deuda",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Registrar un abono a una deuda
        /// </summary>
        [HttpPost("abonar")]
        public async Task<IActionResult> AbonarDeuda([FromBody] JsonElement data)
        {
            try
            {
                var deudaId = Guid.Parse(data.GetProperty("deudaId").GetString());
                var monto = data.GetProperty("monto").GetDecimal();
                var metodoPago = data.GetProperty("metodoPago").GetString();

                // Manejar usuarioId que puede ser null
                Guid? usuarioId = null;
                if (data.TryGetProperty("usuarioId", out var userEl) &&
                    !string.IsNullOrWhiteSpace(userEl.GetString()) &&
                    userEl.GetString() != "null")
                {
                    usuarioId = Guid.Parse(userEl.GetString());
                }

                var notas = data.TryGetProperty("notas", out var notasEl) ? notasEl.GetString() : null;

                var deuda = await _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .FirstOrDefaultAsync(d => d.Id == deudaId);

                if (deuda == null)
                {
                    return NotFound(new { success = false, message = "Deuda no encontrada" });
                }

                if (deuda.Estado == "pagada")
                {
                    return Ok(new { success = false, message = "Esta deuda ya está pagada" });
                }

                if (monto > deuda.Saldo)
                {
                    return Ok(new { success = false, message = "El monto del abono supera el saldo pendiente" });
                }

                // Registrar abono
                var abono = new AbonoDeuda
                {
                    Id = Guid.NewGuid(),
                    DeudaId = deudaId,
                    Monto = monto,
                    MetodoPago = metodoPago,
                    FechaAbono = DateTime.UtcNow,
                    Notas = notas
                };

                _context.AbonosDeuda.Add(abono);

                // Actualizar deuda
                deuda.MontoPagado += monto;
                deuda.Saldo -= monto;

                if (deuda.Saldo <= 0)
                {
                    deuda.Estado = "pagada";
                    deuda.Saldo = 0; // Asegurar que quede en 0
                }
                else if (deuda.FechaVencimiento.HasValue && deuda.FechaVencimiento < DateTime.UtcNow)
                {
                    deuda.Estado = "vencida";
                }

                // Registrar ingreso en caja SIEMPRE
                // SIEMPRE registrar en caja
                {
                    var movimientoCaja = new MovimientoCaja
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Categoria = "abono_deuda",
                        Monto = monto,
                        Descripcion = $"Abono deuda - {deuda.Cliente.Nombre} {deuda.Cliente.Apellido} - {deuda.Concepto}",
                        ReferenciaId = deuda.Id,
                        UsuarioId = usuarioId ?? Guid.Parse("00000000-0000-0000-0000-000000000000"),
                        MetodoPago = metodoPago,
                        Fecha = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.MovimientosCaja.Add(movimientoCaja);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = deuda.Estado == "pagada" ? "Deuda pagada completamente" : "Abono registrado correctamente",
                    data = new
                    {
                        abono,
                        deuda = new
                        {
                            deuda.Id,
                            deuda.Saldo,
                            deuda.MontoPagado,
                            deuda.Estado
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar abono",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Actualizar el estado de una deuda
        /// </summary>
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] JsonElement data)
        {
            try
            {
                var nuevoEstado = data.GetProperty("estado").GetString();

                var deuda = await _context.DeudasClientes.FindAsync(id);
                if (deuda == null)
                {
                    return NotFound(new { success = false, message = "Deuda no encontrada" });
                }

                if (!new[] { "pendiente", "pagada", "vencida" }.Contains(nuevoEstado))
                {
                    return Ok(new { success = false, message = "Estado no válido" });
                }

                deuda.Estado = nuevoEstado;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Estado actualizado correctamente",
                    data = deuda
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al actualizar estado",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Eliminar una deuda
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDeuda(Guid id)
        {
            try
            {
                var deuda = await _context.DeudasClientes
                    .Include(d => d.Abonos)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (deuda == null)
                {
                    return NotFound(new { success = false, message = "Deuda no encontrada" });
                }

                if (deuda.Abonos.Any())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No se puede eliminar una deuda con abonos registrados"
                    });
                }

                _context.DeudasClientes.Remove(deuda);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Deuda eliminada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al eliminar deuda",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener deudas vencidas
        /// </summary>
        [HttpGet("vencidas")]
        public async Task<IActionResult> GetDeudasVencidas()
        {
            try
            {
                var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

                var deudas = await _context.DeudasClientes
                    .Include(d => d.Cliente)
                    .Where(d => d.FechaVencimiento.HasValue &&
                                d.FechaVencimiento < hoy &&
                                d.Estado != "pagada")
                    .OrderBy(d => d.FechaVencimiento)
                    .Select(d => new
                    {
                        d.Id,
                        d.ClienteId,
                        clienteNombre = $"{d.Cliente.Nombre} {d.Cliente.Apellido}",
                        clienteTelefono = d.Cliente.Telefono,
                        d.Concepto,
                        d.MontoTotal,
                        d.Saldo,
                        d.FechaVencimiento,
                        diasVencidos = (hoy - d.FechaVencimiento.Value).Days
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    totalVencidas = deudas.Count,
                    montoTotal = deudas.Sum(d => d.Saldo),
                    data = deudas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener deudas vencidas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener historial de abonos de una deuda
        /// </summary>
        [HttpGet("{deudaId}/abonos")]
        public async Task<IActionResult> GetAbonosDeuda(Guid deudaId)
        {
            try
            {
                var abonos = await _context.AbonosDeuda
                    .Where(a => a.DeudaId == deudaId)
                    .OrderByDescending(a => a.FechaAbono)
                    .ToListAsync();

                var totalAbonado = abonos.Sum(a => a.Monto);

                return Ok(new
                {
                    success = true,
                    totalAbonado,
                    cantidadAbonos = abonos.Count,
                    data = abonos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener abonos",
                    error = ex.Message
                });
            }
        }
    }
}