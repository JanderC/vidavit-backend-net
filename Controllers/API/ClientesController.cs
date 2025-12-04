using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            return await _context.Clientes
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // GET: api/Clientes/{id}
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

        // GET: api/Clientes/buscar?query=texto
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<Cliente>>> BuscarClientes([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Cliente>();

            query = query.ToLower();
            return await _context.Clientes
                .Where(c => c.Nombre.ToLower().Contains(query) ||
                            c.Apellido.ToLower().Contains(query) ||
                            c.Cedula.ToLower().Contains(query))
                .OrderBy(c => c.Nombre)
                .ToListAsync();
        }

        // POST: api/Clientes
        [HttpPost]
        public async Task<ActionResult<Cliente>> CreateCliente([FromBody] Cliente cliente)
        {
            cliente.Id = Guid.NewGuid();
            cliente.CreatedAt = DateTime.UtcNow;
            cliente.UpdatedAt = DateTime.UtcNow;
            cliente.Activo = true;
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, cliente);
        }

        // PUT: api/Clientes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCliente(Guid id, [FromBody] Cliente cliente)
        {
            if (id != cliente.Id)
                return BadRequest();

            var dbCliente = await _context.Clientes.FindAsync(id);
            if (dbCliente == null)
                return NotFound();

            dbCliente.Nombre = cliente.Nombre;
            dbCliente.Apellido = cliente.Apellido;
            dbCliente.Cedula = cliente.Cedula;
            dbCliente.Telefono = cliente.Telefono;
            dbCliente.Email = cliente.Email;
            dbCliente.FechaNacimiento = cliente.FechaNacimiento;
            dbCliente.Direccion = cliente.Direccion;
            dbCliente.HuellaDigital = cliente.HuellaDigital;
            dbCliente.HuellaTemplate = cliente.HuellaTemplate;
            dbCliente.FotoBase64 = cliente.FotoBase64;
            dbCliente.Activo = cliente.Activo;
            dbCliente.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Clientes/{id}
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

        // GET: api/Clientes/{id}/checkins
        [HttpGet("{id}/checkins")]
        public async Task<ActionResult<IEnumerable<CheckIn>>> GetCheckIns(Guid id)
        {
            var checkins = await _context.CheckIns
                .Where(ci => ci.ClienteId == id)
                .OrderByDescending(ci => ci.FechaHora)
                .ToListAsync();

            return checkins;
        }

        // GET: api/Clientes/{id}/membresias
        [HttpGet("{id}/membresias")]
        public async Task<ActionResult<IEnumerable<Membresia>>> GetMembresias(Guid id)
        {
            var membresias = await _context.Membresias
                .Where(m => m.ClienteId == id)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return membresias;
        }
    }
}