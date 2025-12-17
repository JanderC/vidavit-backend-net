using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

namespace VidaFit.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            return await _context.Clientes
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(Guid id)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                .Include(c => c.CheckIns)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
                return NotFound();

            return cliente;
        }

        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente([FromBody] JsonElement data)
        {
            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                Nombre = data.GetProperty("nombre").GetString(),
                Apellido = data.GetProperty("apellido").GetString(),
                Cedula = data.GetProperty("cedula").GetString(),
                Telefono = data.TryGetProperty("telefono", out var tel) && !string.IsNullOrWhiteSpace(tel.GetString()) ? tel.GetString() : null,
                Email = data.TryGetProperty("email", out var email) && !string.IsNullOrWhiteSpace(email.GetString()) ? email.GetString() : null,
                Direccion = data.TryGetProperty("direccion", out var dir) && !string.IsNullOrWhiteSpace(dir.GetString()) ? dir.GetString() : null,
                FotoBase64 = data.TryGetProperty("fotoBase64", out var foto) && !string.IsNullOrWhiteSpace(foto.GetString()) ? foto.GetString() : null,
                HuellaDigital = data.TryGetProperty("huellaDigital", out var hd) ? hd.GetString() : null,
                HuellaTemplate = data.TryGetProperty("huellaTemplate", out var ht) ? ht.GetString() : null,
                Activo = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (data.TryGetProperty("fechaNacimiento", out var fechaNac) && !string.IsNullOrEmpty(fechaNac.GetString()))
            {
                if (DateTime.TryParse(fechaNac.GetString(), out DateTime fechaPost))
                {
                    cliente.FechaNacimiento = DateTime.SpecifyKind(fechaPost, DateTimeKind.Utc);
                }
            }

            var existente = await _context.Clientes.FirstOrDefaultAsync(c => c.Cedula == cliente.Cedula);
            if (existente != null)
                return BadRequest("Ya existe un cliente con esa cédula");

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, cliente);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(Guid id, [FromBody] JsonElement data)
        {
            if (!data.TryGetProperty("id", out var idProp))
                return BadRequest("El ID es requerido");

            Guid dataId = Guid.Parse(idProp.GetString());
            if (id != dataId)
                return BadRequest("El ID no coincide");

            var dbCliente = await _context.Clientes.FindAsync(id);
            if (dbCliente == null)
                return NotFound();

            string cedula = data.GetProperty("cedula").GetString();
            var cedulaExistente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Cedula == cedula && c.Id != id);

            if (cedulaExistente != null)
                return BadRequest("Ya existe otro cliente con esa cédula");

            dbCliente.Nombre = data.GetProperty("nombre").GetString();
            dbCliente.Apellido = data.GetProperty("apellido").GetString();
            dbCliente.Cedula = cedula;
            dbCliente.Telefono = data.TryGetProperty("telefono", out var tel) && !string.IsNullOrWhiteSpace(tel.GetString()) ? tel.GetString() : null;
            dbCliente.Email = data.TryGetProperty("email", out var email) && !string.IsNullOrWhiteSpace(email.GetString()) ? email.GetString() : null;
            dbCliente.Direccion = data.TryGetProperty("direccion", out var dir) && !string.IsNullOrWhiteSpace(dir.GetString()) ? dir.GetString() : null;
            dbCliente.Activo = data.GetProperty("activo").GetBoolean();
            dbCliente.UpdatedAt = DateTime.UtcNow;

            if (data.TryGetProperty("fechaNacimiento", out var fechaNac) && !string.IsNullOrEmpty(fechaNac.GetString()))
            {
                if (DateTime.TryParse(fechaNac.GetString(), out DateTime fechaPut))
                {
                    dbCliente.FechaNacimiento = DateTime.SpecifyKind(fechaPut, DateTimeKind.Utc);
                }
            }
            else
            {
                dbCliente.FechaNacimiento = null;
            }

            if (data.TryGetProperty("fotoBase64", out var foto) && !string.IsNullOrWhiteSpace(foto.GetString()))
            {
                dbCliente.FotoBase64 = foto.GetString();
            }

            if (data.TryGetProperty("huellaTemplate", out var ht) && !string.IsNullOrWhiteSpace(ht.GetString()))
            {
                dbCliente.HuellaTemplate = ht.GetString();
            }

            if (data.TryGetProperty("huellaDigital", out var hd) && !string.IsNullOrWhiteSpace(hd.GetString()))
            {
                dbCliente.HuellaDigital = hd.GetString();
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClienteExists(id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/huella")]
        public async Task<IActionResult> GuardarHuella(Guid id, [FromBody] JsonElement data)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            cliente.HuellaTemplate = data.TryGetProperty("huellaTemplate", out var ht) ? ht.GetString() : null;
            cliente.HuellaDigital = data.TryGetProperty("huellaDigital", out var hd) ? hd.GetString() : null;
            cliente.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Huella guardada exitosamente" });
        }

        [HttpGet("{id}/membresias")]
        public async Task<ActionResult<IEnumerable<object>>> GetMembresiasByCliente(Guid id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            var membresias = await _context.Membresias
                .Include(m => m.Plan)
                .Where(m => m.ClienteId == id)
                .OrderByDescending(m => m.FechaInicio)
                .Select(m => new
                {
                    id = m.Id,
                    planId = m.PlanId,
                    planNombre = m.Plan.Nombre,
                    planColor = m.Plan.Color,
                    fechaInicio = m.FechaInicio,
                    fechaVencimiento = m.FechaVencimiento,
                    estado = m.Estado,
                    montoPagado = m.MontoPagado,
                    metodoPago = m.MetodoPago,
                    notas = m.Notas,
                    diasRestantes = m.Estado == "activa" ? (m.FechaVencimiento - DateTime.UtcNow).Days : 0
                })
                .ToListAsync();

            return Ok(membresias);
        }

        private bool ClienteExists(Guid id)
        {
            return _context.Clientes.Any(e => e.Id == id);
        }
    }
}