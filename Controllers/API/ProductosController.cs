using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Productos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        // GET: api/Productos/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
                return NotFound();

            return producto;
        }

        // GET: api/Productos/activos
        [HttpGet("activos")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosActivos()
        {
            return await _context.Productos
                .Where(p => p.Activo && p.Stock > 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        // GET: api/Productos/categoria/{categoria}
        [HttpGet("categoria/{categoria}")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosPorCategoria(string categoria)
        {
            return await _context.Productos
                .Where(p => p.Categoria == categoria && p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        // DTO para crear/actualizar productos
        public class ProductoDto
        {
            public string Nombre { get; set; }
            public string? Descripcion { get; set; }
            public decimal Precio { get; set; }
            public int Stock { get; set; }
            public string? ImagenBase64 { get; set; }
            public string? Categoria { get; set; }
            public bool Activo { get; set; } = true;
        }

        // POST: api/Productos
        [HttpPost]
        public async Task<ActionResult<Producto>> CreateProducto([FromBody] ProductoDto productoDto)
        {
            if (string.IsNullOrWhiteSpace(productoDto.Nombre))
                return BadRequest("El nombre del producto es requerido");

            if (productoDto.Precio <= 0)
                return BadRequest("El precio debe ser mayor a 0");

            if (productoDto.Stock < 0)
                return BadRequest("El stock no puede ser negativo");

            var producto = new Producto
            {
                Id = Guid.NewGuid(),
                Nombre = productoDto.Nombre,
                Descripcion = productoDto.Descripcion,
                Precio = productoDto.Precio,
                Stock = productoDto.Stock,
                ImagenBase64 = productoDto.ImagenBase64,
                Categoria = productoDto.Categoria,
                Activo = productoDto.Activo,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        // PUT: api/Productos/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProducto(Guid id, [FromBody] Producto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID del producto no coincide");

            var dbProducto = await _context.Productos.FindAsync(id);
            if (dbProducto == null)
                return NotFound("Producto no encontrado");

            if (string.IsNullOrWhiteSpace(producto.Nombre))
                return BadRequest("El nombre del producto es requerido");

            if (producto.Precio <= 0)
                return BadRequest("El precio debe ser mayor a 0");

            if (producto.Stock < 0)
                return BadRequest("El stock no puede ser negativo");

            dbProducto.Nombre = producto.Nombre;
            dbProducto.Descripcion = producto.Descripcion;
            dbProducto.Precio = producto.Precio;
            dbProducto.Stock = producto.Stock;
            dbProducto.ImagenBase64 = producto.ImagenBase64;
            dbProducto.Categoria = producto.Categoria;
            dbProducto.Activo = producto.Activo;
            dbProducto.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Productos/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound("Producto no encontrado");

            // Verificar si el producto tiene ventas asociadas
            var tieneVentas = await _context.VentasProductos
                .AnyAsync(v => v.ProductoId == id);

            if (tieneVentas)
            {
                return BadRequest(
                    "No se puede eliminar el producto porque tiene ventas registradas. " +
                    "Puede desactivarlo en su lugar."
                );
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Producto eliminado exitosamente" });
        }

        // PATCH: api/Productos/{id}/toggle-activo
        [HttpPatch("{id}/toggle-activo")]
        public async Task<IActionResult> ToggleActivo(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound("Producto no encontrado");

            producto.Activo = !producto.Activo;
            producto.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Producto {(producto.Activo ? "activado" : "desactivado")} exitosamente",
                activo = producto.Activo
            });
        }

        // PATCH: api/Productos/{id}/actualizar-stock
        [HttpPatch("{id}/actualizar-stock")]
        public async Task<IActionResult> ActualizarStock(Guid id, [FromBody] StockUpdateDto stockDto)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound("Producto no encontrado");

            if (stockDto.NuevoStock < 0)
                return BadRequest("El stock no puede ser negativo");

            producto.Stock = stockDto.NuevoStock;
            producto.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Stock actualizado exitosamente",
                stock = producto.Stock
            });
        }

        public class StockUpdateDto
        {
            public int NuevoStock { get; set; }
        }

        // GET: api/Productos/stock-bajo
        [HttpGet("stock-bajo")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosStockBajo([FromQuery] int limite = 10)
        {
            return await _context.Productos
                .Where(p => p.Activo && p.Stock < limite)
                .OrderBy(p => p.Stock)
                .ToListAsync();
        }

        // GET: api/Productos/categorias
        [HttpGet("categorias")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategorias()
        {
            var categorias = await _context.Productos
                .Where(p => !string.IsNullOrEmpty(p.Categoria))
                .Select(p => p.Categoria)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(categorias);
        }

        // GET: api/Productos/buscar
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<Producto>>> BuscarProductos([FromQuery] string termino)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return BadRequest("Debe proporcionar un término de búsqueda");

            var terminoLower = termino.ToLower();

            var productos = await _context.Productos
                .Where(p =>
                    p.Nombre.ToLower().Contains(terminoLower) ||
                    (p.Descripcion != null && p.Descripcion.ToLower().Contains(terminoLower)) ||
                    (p.Categoria != null && p.Categoria.ToLower().Contains(terminoLower))
                )
                .OrderByDescending(p => p.Activo)
                .ThenBy(p => p.Nombre)
                .ToListAsync();

            return Ok(productos);
        }
    }
}