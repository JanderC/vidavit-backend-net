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

        // POST: api/Productos
        [HttpPost]
        public async Task<ActionResult<Producto>> CreateProducto([FromBody] Producto producto)
        {
            producto.Id = Guid.NewGuid();
            producto.CreatedAt = DateTime.UtcNow;
            producto.UpdatedAt = DateTime.UtcNow;
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        // PUT: api/Productos/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProducto(Guid id, [FromBody] Producto producto)
        {
            if (id != producto.Id)
                return BadRequest();

            var dbProducto = await _context.Productos.FindAsync(id);
            if (dbProducto == null)
                return NotFound();

            dbProducto.Nombre = producto.Nombre;
            dbProducto.Descripcion = producto.Descripcion;
            dbProducto.Precio = producto.Precio;
            dbProducto.Stock = producto.Stock;
            dbProducto.ImagenBase64 = producto.ImagenBase64;
            dbProducto.Categoria = producto.Categoria;
            dbProducto.Activo = producto.Activo;
            dbProducto.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Productos/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return NoContent();
        }

       
    }
}