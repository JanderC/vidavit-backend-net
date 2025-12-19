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
                .OrderByDescending(v => v.FechaVenta)
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

        // GET: api/VentasProductos/cliente/{clienteId}
        [HttpGet("cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<VentaProducto>>> GetVentasPorCliente(Guid clienteId)
        {
            return await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.ClienteId == clienteId)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();
        }

        // DTO para crear ventas (sin necesidad de objetos completos)
        public class CreateVentaDto
        {
            public Guid ClienteId { get; set; }
            public Guid ProductoId { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Total { get; set; }
            public string EstadoPago { get; set; } = "pendiente";
            public string? Notas { get; set; }
        }

        // POST: api/VentasProductos
        [HttpPost]
        public async Task<ActionResult<VentaProducto>> CreateVentaProducto([FromBody] CreateVentaDto ventaDto)
        {
            // Validar que el cliente existe
            var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == ventaDto.ClienteId);
            if (!clienteExiste)
                return BadRequest("El cliente no existe");

            // Validar que el producto existe
            var producto = await _context.Productos.FindAsync(ventaDto.ProductoId);
            if (producto == null)
                return BadRequest("El producto no existe");

            // Validar stock
            if (producto.Stock < ventaDto.Cantidad)
                return BadRequest($"Stock insuficiente. Disponible: {producto.Stock}");

            // Crear la venta
            var venta = new VentaProducto
            {
                Id = Guid.NewGuid(),
                ClienteId = ventaDto.ClienteId,
                ProductoId = ventaDto.ProductoId,
                Cantidad = ventaDto.Cantidad,
                PrecioUnitario = ventaDto.PrecioUnitario,
                Total = ventaDto.Total > 0 ? ventaDto.Total : ventaDto.Cantidad * ventaDto.PrecioUnitario,
                EstadoPago = ventaDto.EstadoPago,
                Notas = ventaDto.Notas,
                FechaVenta = DateTime.UtcNow,
                FechaPago = ventaDto.EstadoPago == "pagado" ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Actualizar stock del producto
            producto.Stock -= venta.Cantidad;
            producto.UpdatedAt = DateTime.UtcNow;

            _context.VentasProductos.Add(venta);
            await _context.SaveChangesAsync();

            // Cargar las relaciones para devolver el objeto completo
            await _context.Entry(venta)
                .Reference(v => v.Cliente)
                .LoadAsync();
            await _context.Entry(venta)
                .Reference(v => v.Producto)
                .LoadAsync();

            return CreatedAtAction(nameof(GetVentaProducto), new { id = venta.Id }, venta);
        }

        // PUT: api/VentasProductos/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVentaProducto(Guid id, [FromBody] CreateVentaDto ventaDto)
        {
            var dbVenta = await _context.VentasProductos.FindAsync(id);
            if (dbVenta == null)
                return NotFound();

            // Restaurar stock anterior
            var productoAnterior = await _context.Productos.FindAsync(dbVenta.ProductoId);
            if (productoAnterior != null)
            {
                productoAnterior.Stock += dbVenta.Cantidad;
            }

            // Validar nuevo producto
            var productoNuevo = await _context.Productos.FindAsync(ventaDto.ProductoId);
            if (productoNuevo == null)
                return BadRequest("El producto no existe");

            if (productoNuevo.Stock < ventaDto.Cantidad)
                return BadRequest($"Stock insuficiente. Disponible: {productoNuevo.Stock}");

            // Actualizar venta
            dbVenta.ClienteId = ventaDto.ClienteId;
            dbVenta.ProductoId = ventaDto.ProductoId;
            dbVenta.Cantidad = ventaDto.Cantidad;
            dbVenta.PrecioUnitario = ventaDto.PrecioUnitario;
            dbVenta.Total = ventaDto.Total > 0 ? ventaDto.Total : ventaDto.Cantidad * ventaDto.PrecioUnitario;
            dbVenta.EstadoPago = ventaDto.EstadoPago;
            dbVenta.Notas = ventaDto.Notas;
            dbVenta.UpdatedAt = DateTime.UtcNow;

            if (ventaDto.EstadoPago == "pagado" && dbVenta.FechaPago == null)
            {
                dbVenta.FechaPago = DateTime.UtcNow;
            }

            // Actualizar stock del nuevo producto
            productoNuevo.Stock -= ventaDto.Cantidad;
            productoNuevo.UpdatedAt = DateTime.UtcNow;

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

            // Devolver el stock al producto
            var producto = await _context.Productos.FindAsync(venta.ProductoId);
            if (producto != null)
            {
                producto.Stock += venta.Cantidad;
                producto.UpdatedAt = DateTime.UtcNow;
            }

            _context.VentasProductos.Remove(venta);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/VentasProductos/{id}/marcar-pagado
        [HttpPost("{id}/marcar-pagado")]
        public async Task<IActionResult> MarcarComoPagado(Guid id)
        {
            var venta = await _context.VentasProductos.FindAsync(id);
            if (venta == null)
                return NotFound();

            venta.EstadoPago = "pagado";
            venta.FechaPago = DateTime.UtcNow;
            venta.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Venta marcada como pagada exitosamente" });
        }

        // GET: api/VentasProductos/cuentas-pendientes
        [HttpGet("cuentas-pendientes")]
        public async Task<ActionResult<IEnumerable<object>>> GetCuentasPendientes()
        {
            var cuentasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente")
                .GroupBy(v => v.ClienteId)
                .Select(g => new
                {
                    Cliente = g.First().Cliente,
                    TotalPendiente = g.Sum(v => v.Total),
                    CantidadVentas = g.Count(),
                    Productos = g.Select(v => v.Producto.Nombre).ToList()
                })
                .OrderByDescending(c => c.TotalPendiente)
                .ToListAsync();

            return Ok(cuentasPendientes);
        }
    }
}