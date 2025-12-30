using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using System.Text.Json;

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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VentaProducto>>> GetVentasProductos()
        {
            return await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();
        }

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

        // DTO para crear ventas con pago parcial
        public class CreateVentaDto
        {
            public Guid? ClienteId { get; set; }
            public Guid ProductoId { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Total { get; set; }
            public string EstadoPago { get; set; } = "pendiente";
            public decimal? MontoPagado { get; set; } // NUEVO: para pago parcial
            public string? Notas { get; set; }
            public Guid? UsuarioId { get; set; } // NUEVO: para registrar en caja
        }

        [HttpPost]
        public async Task<IActionResult> CreateVentaProducto([FromBody] CreateVentaDto ventaDto)
        {
            try
            {
                // Validar cliente (OPCIONAL - puede ser null para ventas rápidas)
                Cliente? cliente = null;
                if (ventaDto.ClienteId.HasValue)
                {
                    cliente = await _context.Clientes.FindAsync(ventaDto.ClienteId.Value);
                    if (cliente == null)
                        return Ok(new { success = false, message = "El cliente especificado no existe" });
                }

                // Validar producto
                var producto = await _context.Productos.FindAsync(ventaDto.ProductoId);
                if (producto == null)
                    return Ok(new { success = false, message = "El producto no existe" });

                // Validar stock
                if (producto.Stock < ventaDto.Cantidad)
                    return Ok(new { success = false, message = $"Stock insuficiente. Disponible: {producto.Stock}" });

                // Calcular totales
                decimal totalVenta = ventaDto.Cantidad * ventaDto.PrecioUnitario;
                decimal montoPagado = ventaDto.MontoPagado ?? totalVenta;

                // Validar que el monto pagado no sea mayor al total
                if (montoPagado > totalVenta)
                    return Ok(new { success = false, message = "El monto pagado no puede ser mayor al total de la venta" });

                // Determinar estado de pago
                string estadoPago;
                if (montoPagado == 0)
                    estadoPago = "pendiente";
                else if (montoPagado < totalVenta)
                    estadoPago = "parcial"; // NUEVO ESTADO
                else
                    estadoPago = "pagado";

                // Crear la venta
                var venta = new VentaProducto
                {
                    Id = Guid.NewGuid(),
                    ClienteId = ventaDto.ClienteId, // NULL si no hay cliente
                    ProductoId = ventaDto.ProductoId,
                    Cantidad = ventaDto.Cantidad,
                    PrecioUnitario = ventaDto.PrecioUnitario,
                    Total = totalVenta,
                    EstadoPago = estadoPago,
                    Notas = ventaDto.Notas,
                    FechaVenta = DateTime.UtcNow,
                    FechaPago = estadoPago == "pagado" ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.VentasProductos.Add(venta);

                // Actualizar stock
                producto.Stock -= venta.Cantidad;
                producto.UpdatedAt = DateTime.UtcNow;

                // SI HAY DEUDA (pago parcial o pendiente), registrarla
                decimal saldoPendiente = totalVenta - montoPagado;
                Guid? deudaId = null;

                if (saldoPendiente > 0)
                {
                    var deuda = new DeudaCliente
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = ventaDto.ClienteId ?? Guid.Empty,
                        Concepto = $"Venta de {producto.Nombre} (x{ventaDto.Cantidad})",
                        MontoTotal = saldoPendiente,
                        MontoPagado = 0,
                        Saldo = saldoPendiente,
                        Estado = "pendiente",
                        FechaCreacion = DateTime.UtcNow,
                        FechaVencimiento = null,
                        Notas = montoPagado > 0
                            ? $"Abono inicial: ${montoPagado:N2} de ${totalVenta:N2}"
                            : "Venta fiada - Sin pago inicial",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.DeudasClientes.Add(deuda);
                    deudaId = deuda.Id;
                }

                // Registrar ingreso en caja (solo si hubo pago)
                if (montoPagado > 0)
                {
                    var movimientoCaja = new MovimientoCaja
                    {
                        Id = Guid.NewGuid(),
                        Tipo = "ingreso",
                        Categoria = "producto",
                        Monto = montoPagado,
                        Descripcion = cliente != null
                            ? $"Venta de {producto.Nombre} (x{ventaDto.Cantidad}) - {cliente.Nombre} {cliente.Apellido}"
                            : $"Venta de {producto.Nombre} (x{ventaDto.Cantidad}) - Venta rápida" +
                                    (saldoPendiente > 0 ? $" (Pago parcial, saldo: ${saldoPendiente:N2})" : ""),
                        ReferenciaId = venta.Id,
                        UsuarioId = ventaDto.UsuarioId ?? Guid.Parse("00000000-0000-0000-0000-000000000000"),
                        MetodoPago = "efectivo", // Podrías agregar esto al DTO
                        Fecha = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.MovimientosCaja.Add(movimientoCaja);
                }

                await _context.SaveChangesAsync();

                // Cargar relaciones
                await _context.Entry(venta).Reference(v => v.Cliente).LoadAsync();
                await _context.Entry(venta).Reference(v => v.Producto).LoadAsync();

                return Ok(new
                {
                    success = true,
                    data = venta,
                    deudaId = deudaId,
                    saldoPendiente = saldoPendiente,
                    message = saldoPendiente > 0
                        ? $"Venta registrada. Saldo pendiente: ${saldoPendiente:N2}"
                        : "Venta registrada exitosamente"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error al crear venta",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

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
            dbVenta.ClienteId = ventaDto.ClienteId; // NULL si no hay cliente
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVentaProducto(Guid id)
        {
            var venta = await _context.VentasProductos.FindAsync(id);
            if (venta == null)
                return NotFound();

            // Devolver stock
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

        [HttpGet("cuentas-pendientes")]
        public async Task<ActionResult<IEnumerable<object>>> GetCuentasPendientes()
        {
            var cuentasPendientes = await _context.VentasProductos
                .Include(v => v.Cliente)
                .Include(v => v.Producto)
                .Where(v => v.EstadoPago == "pendiente" || v.EstadoPago == "parcial")
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