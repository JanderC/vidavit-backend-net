using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpleadosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EmpleadosController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtener todos los empleados
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEmpleados([FromQuery] bool? activo = null)
        {
            try
            {
                var query = _context.Empleados.AsQueryable();

                if (activo.HasValue)
                {
                    query = query.Where(e => e.Activo == activo.Value);
                }

                var empleados = await query
                    .OrderBy(e => e.Nombre)
                    .ToListAsync();

                return Ok(new { success = true, data = empleados });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener empleados",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener un empleado por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmpleado(Guid id)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);

                if (empleado == null)
                {
                    return NotFound(new { success = false, message = "Empleado no encontrado" });
                }

                return Ok(new { success = true, data = empleado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener empleado",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Crear un nuevo empleado
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateEmpleado([FromBody] JsonElement data)
        {
            try
            {
                var nombre = data.GetProperty("nombre").GetString();
                var apellido = data.GetProperty("apellido").GetString();
                var cedula = data.GetProperty("cedula").GetString();
                var telefono = data.TryGetProperty("telefono", out var telEl) ? telEl.GetString() : null;
                var email = data.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
                var cargo = data.TryGetProperty("cargo", out var cargoEl) ? cargoEl.GetString() : null;
                var salario = data.TryGetProperty("salario", out var salEl) ? (decimal?)salEl.GetDecimal() : null;

                DateTime? fechaContratacion = null;
                if (data.TryGetProperty("fechaContratacion", out var fechaEl) && !string.IsNullOrWhiteSpace(fechaEl.GetString()))
                {
                    fechaContratacion = DateTime.SpecifyKind(DateTime.Parse(fechaEl.GetString()), DateTimeKind.Utc);
                }

                // Validar cédula única
                var existeCedula = await _context.Empleados.AnyAsync(e => e.Cedula == cedula);
                if (existeCedula)
                {
                    return Ok(new { success = false, message = "Ya existe un empleado con esta cédula" });
                }

                var empleado = new Empleado
                {
                    Id = Guid.NewGuid(),
                    Nombre = nombre,
                    Apellido = apellido,
                    Cedula = cedula,
                    Telefono = telefono,
                    Email = email,
                    Cargo = cargo,
                    Salario = salario,
                    FechaContratacion = fechaContratacion ?? DateTime.Now,
                    Activo = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Empleados.Add(empleado);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Empleado creado correctamente",
                    data = empleado
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al crear empleado",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Actualizar un empleado
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmpleado(Guid id, [FromBody] JsonElement data)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);
                if (empleado == null)
                {
                    return NotFound(new { success = false, message = "Empleado no encontrado" });
                }

                if (data.TryGetProperty("nombre", out var nomEl))
                    empleado.Nombre = nomEl.GetString();
                if (data.TryGetProperty("apellido", out var apeEl))
                    empleado.Apellido = apeEl.GetString();
                if (data.TryGetProperty("cedula", out var cedEl))
                    empleado.Cedula = cedEl.GetString();
                if (data.TryGetProperty("telefono", out var telEl))
                    empleado.Telefono = telEl.GetString();
                if (data.TryGetProperty("email", out var emailEl))
                    empleado.Email = emailEl.GetString();
                if (data.TryGetProperty("cargo", out var cargoEl))
                    empleado.Cargo = cargoEl.GetString();
                if (data.TryGetProperty("salario", out var salEl))
                    empleado.Salario = salEl.GetDecimal();
                if (data.TryGetProperty("activo", out var actEl))
                    empleado.Activo = actEl.GetBoolean();

                empleado.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Empleado actualizado correctamente",
                    data = empleado
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al actualizar empleado",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Desactivar un empleado
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmpleado(Guid id)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);
                if (empleado == null)
                {
                    return NotFound(new { success = false, message = "Empleado no encontrado" });
                }

                empleado.Activo = false;
                empleado.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Empleado desactivado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al desactivar empleado",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Registrar pago de salario a un empleado
        /// </summary>
        [HttpPost("pagar")]
        public async Task<IActionResult> PagarSalario([FromBody] JsonElement data)
        {
            try
            {
                var empleadoId = Guid.Parse(data.GetProperty("empleadoId").GetString());
                var monto = data.GetProperty("monto").GetDecimal();
                var periodo = data.GetProperty("periodo").GetString();
                var metodoPago = data.GetProperty("metodoPago").GetString();
                var usuarioId = Guid.Parse(data.GetProperty("usuarioId").GetString());
                var notas = data.TryGetProperty("notas", out var notasEl) ? notasEl.GetString() : null;

                var empleado = await _context.Empleados.FindAsync(empleadoId);
                if (empleado == null || !empleado.Activo)
                {
                    return Ok(new { success = false, message = "Empleado no encontrado o inactivo" });
                }

                // Registrar pago a empleado
                var pago = new PagoEmpleado
                {
                    Id = Guid.NewGuid(),
                    EmpleadoId = empleadoId,
                    Monto = monto,
                    Periodo = periodo,
                    FechaPago = DateTime.Now,
                    MetodoPago = metodoPago,
                    Notas = notas,
                    CreatedAt = DateTime.Now
                };

                _context.PagosEmpleados.Add(pago);

                // Registrar egreso en caja
                var movimientoCaja = new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    Tipo = "egreso",
                    Categoria = "salario",
                    Monto = monto,
                    Descripcion = $"Pago salario {periodo} - {empleado.Nombre} {empleado.Apellido}",
                    ReferenciaId = pago.Id,
                    UsuarioId = usuarioId,
                    MetodoPago = metodoPago,
                    Fecha = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.MovimientosCaja.Add(movimientoCaja);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Pago registrado correctamente",
                    data = pago
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al registrar pago",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Obtener historial de pagos de un empleado
        /// </summary>
        [HttpGet("{empleadoId}/pagos")]
        public async Task<IActionResult> GetPagosEmpleado(Guid empleadoId)
        {
            try
            {
                var pagos = await _context.PagosEmpleados
                    .Include(p => p.Empleado)
                    .Where(p => p.EmpleadoId == empleadoId)
                    .OrderByDescending(p => p.FechaPago)
                    .Select(p => new
                    {
                        p.Id,
                        p.Monto,
                        p.Periodo,
                        p.FechaPago,
                        p.MetodoPago,
                        p.Notas,
                        empleado = $"{p.Empleado.Nombre} {p.Empleado.Apellido}"
                    })
                    .ToListAsync();

                var totalPagado = pagos.Sum(p => p.Monto);

                return Ok(new
                {
                    success = true,
                    totalPagado,
                    cantidadPagos = pagos.Count,
                    data = pagos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener pagos",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener todos los pagos realizados a empleados
        /// </summary>
        [HttpGet("pagos")]
        public async Task<IActionResult> GetTodosPagos([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            try
            {
                var query = _context.PagosEmpleados
                    .Include(p => p.Empleado)
                    .AsQueryable();

                if (desde.HasValue)
                {
                    var inicio = DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Utc);
                    query = query.Where(p => p.FechaPago >= inicio);
                }

                if (hasta.HasValue)
                {
                    var fin = DateTime.SpecifyKind(hasta.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                    query = query.Where(p => p.FechaPago <= fin);
                }

                var pagos = await query
                    .OrderByDescending(p => p.FechaPago)
                    .Select(p => new
                    {
                        p.Id,
                        p.EmpleadoId,
                        empleado = $"{p.Empleado.Nombre} {p.Empleado.Apellido}",
                        p.Monto,
                        p.Periodo,
                        p.FechaPago,
                        p.MetodoPago,
                        p.Notas
                    })
                    .ToListAsync();

                var totalPagado = pagos.Sum(p => p.Monto);

                return Ok(new
                {
                    success = true,
                    totalPagado,
                    cantidadPagos = pagos.Count,
                    data = pagos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener pagos",
                    error = ex.Message
                });
            }
        }
    }
}