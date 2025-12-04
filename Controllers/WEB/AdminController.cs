using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Admin/usuarios
        [HttpGet("usuarios")]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetUsuarios()
        {
            return await _context.Usuarios
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        // GET: api/Admin/usuarios/{id}
        [HttpGet("usuarios/{id}")]
        public async Task<ActionResult<Usuario>> GetUsuario(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            return usuario;
        }

        // POST: api/Admin/usuarios
        [HttpPost("usuarios")]
        public async Task<ActionResult<Usuario>> CreateUsuario([FromBody] Usuario usuario)
        {
            usuario.Id = Guid.NewGuid();
            usuario.CreatedAt = DateTime.UtcNow;
            usuario.UpdatedAt = DateTime.UtcNow;
            usuario.Activo = true;
            // Aquí deberías hashear la contraseña antes de guardarla
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, usuario);
        }

        // PUT: api/Admin/usuarios/{id}
        [HttpPut("usuarios/{id}")]
        public async Task<IActionResult> UpdateUsuario(Guid id, [FromBody] Usuario usuario)
        {
            if (id != usuario.Id)
                return BadRequest();

            var dbUsuario = await _context.Usuarios.FindAsync(id);
            if (dbUsuario == null)
                return NotFound();

            dbUsuario.Nombre = usuario.Nombre;
            dbUsuario.Email = usuario.Email;
            dbUsuario.Rol = usuario.Rol;
            dbUsuario.Telefono = usuario.Telefono;
            dbUsuario.Activo = usuario.Activo;
            dbUsuario.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Admin/usuarios/{id}
        [HttpDelete("usuarios/{id}")]
        public async Task<IActionResult> DeleteUsuario(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/Admin/usuarios/{id}/cambiar-password
        [HttpPut("usuarios/{id}/cambiar-password")]
        public async Task<IActionResult> CambiarPassword(Guid id, [FromBody] CambiarPasswordRequest request)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            // Aquí deberías hashear la nueva contraseña antes de guardarla
            usuario.PasswordHash = request.NuevaPassword; // Reemplaza por hash real
            usuario.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Contraseña actualizada correctamente" });
        }
    }

    public class CambiarPasswordRequest
    {
        public string NuevaPassword { get; set; }
    }
}