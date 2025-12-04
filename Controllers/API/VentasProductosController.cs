using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;


namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasProductosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VentasProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/VentasProductos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VentaProducto>>> GetVentasProductos()
        {
            return await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        // GET: api/VentasProductos/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<VentaProducto>> GetVentaProducto(Guid id)
        {
            var venta = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            return venta;
        }

        // POST: api/VentasProductos
        [HttpPost]
        public async Task<ActionResult<VentaProducto>> CreateVentaProducto([FromBody] VentaProducto venta)
        {
            venta.Id = Guid.NewGuid();
            venta.CreatedAt = DateTime.UtcNow;
            venta.UpdatedAt = DateTime.UtcNow;
            venta.FechaVenta = DateTime.UtcNow;
            _context.VentasProductos.Add(venta);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetVentaProducto), new { id = venta.Id }, venta);
        }

        // PUT: api/VentasProductos/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVentaProducto(Guid id, [FromBody] VentaProducto venta)
        {
            if (id != venta.Id)
                return BadRequest();

            var dbVenta = await _context.VentasProductos.FindAsync(id);
            if (dbVenta == null)
                return NotFound();

            dbVenta.ClienteId = venta.ClienteId;
            dbVenta.ProductoId = venta.ProductoId;
            dbVenta.Cantidad = venta.Cantidad;
            dbVenta.PrecioUnitario = venta.PrecioUnitario;
            dbVenta.Total = venta.Total;
            dbVenta.EstadoPago = venta.EstadoPago;
            dbVenta.FechaVenta = venta.FechaVenta;
            dbVenta.FechaPago = venta.FechaPago;
            dbVenta.Notas = venta.Notas;
            dbVenta.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/VentasProductos/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVentaProducto(Guid id)
        {
            var venta = await _context.VentasProductos.FindAsync(id);
            if (venta == null)
                return NotFound();

            _context.VentasProductos.Remove(venta);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}