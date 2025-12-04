using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VidaFit.Services;
using VidaFit.Data;
using Microsoft.EntityFrameworkCore;
using VidaFitBackend.Models;


namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _context;

        public AuthController(IAuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        /// <summary>
        /// Iniciar sesión con email y contraseña
        /// </summary>
        /// <param name="request">Credenciales de login</param>
        /// <returns>Token JWT y datos del usuario</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email y contraseña son requeridos"
                });
            }

            try
            {
                var result = await _authService.Login(request.Email, request.Password);


                if (!result.Success)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                return Ok(new
                {
                    success = true,
                    token = result.Token,
                    usuario = result.Usuario
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error en el servidor",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtener información del usuario autenticado
        /// </summary>
        /// <returns>Datos del usuario actual</returns>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Token inválido o expirado"
                    });
                }

                var usuario = await _authService.GetUsuarioByEmail(email);

                if (usuario == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Usuario no encontrado"
                    });
                }

                return Ok(new
                {
                    success = true,
                    usuario = new
                    {
                        id = usuario.Id,
                        nombre = usuario.Nombre,
                        email = usuario.Email,
                        rol = usuario.Rol,
                        activo = usuario.Activo
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo usuario",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Cerrar sesión (en JWT es manejado por el cliente)
        /// </summary>
        /// <returns>Confirmación de logout</returns>
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // En JWT, el logout se maneja en el cliente eliminando el token
            // Este endpoint es principalmente para registrar la acción

            return Ok(new
            {
                success = true,
                message = "Sesión cerrada exitosamente"
            });
        }

        /// <summary>
        /// Cambiar contraseña del usuario autenticado
        /// </summary>
        /// <param name="request">Contraseña actual y nueva</param>
        /// <returns>Confirmación del cambio</returns>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Token inválido"
                    });
                }

                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (usuario == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Usuario no encontrado"
                    });
                }

                // Verificar contraseña actual (temporal - implementar BCrypt después)
                if (usuario.PasswordHash != request.CurrentPassword)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Contraseña actual incorrecta"
                    });
                }

                // Actualizar contraseña (temporal - implementar BCrypt después)
                usuario.PasswordHash = request.NewPassword;
                usuario.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Contraseña actualizada exitosamente"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al cambiar contraseña",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verificar si un token es válido
        /// </summary>
        /// <returns>Estado del token</returns>
        [HttpGet("verify")]
        [Authorize]
        public IActionResult VerifyToken()
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var nombre = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            return Ok(new
            {
                success = true,
                valid = true,
                email,
                nombre,
                rol
            });
        }

        /// <summary>
        /// Registrar nuevo usuario (solo para admins)
        /// </summary>
        /// <param name="request">Datos del nuevo usuario</param>
        /// <returns>Usuario creado</returns>
        [HttpPost("register")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Verificar si el email ya existe
                var existingUser = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "El email ya está registrado"
                    });
                }

                // Crear nuevo usuario
                var nuevoUsuario = new Usuario
                {
                    Nombre = request.Nombre,
                    Email = request.Email,
                    PasswordHash = request.Password, // TODO: Implementar BCrypt
                    Rol = request.Rol ?? "staff",
                    Telefono = request.Telefono,
                    Activo = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Usuarios.Add(nuevoUsuario);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Usuario registrado exitosamente",
                    usuario = new
                    {
                        id = nuevoUsuario.Id,
                        nombre = nuevoUsuario.Nombre,
                        email = nuevoUsuario.Email,
                        rol = nuevoUsuario.Rol
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al registrar usuario",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Listar todos los usuarios (solo para admins)
        /// </summary>
        /// <returns>Lista de usuarios</returns>
        [HttpGet("users")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var usuarios = await _context.Usuarios
                    .Where(u => u.Activo)
                    .Select(u => new
                    {
                        u.Id,
                        u.Nombre,
                        u.Email,
                        u.Rol,
                        u.Telefono,
                        u.Activo,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    usuarios
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al obtener usuarios",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Desactivar usuario (solo para admins)
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Confirmación</returns>
        [HttpDelete("users/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeactivateUser(Guid id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Usuario no encontrado"
                    });
                }

                usuario.Activo = false;
                usuario.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Usuario desactivado exitosamente"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al desactivar usuario",
                    error = ex.Message
                });
            }
        }
    }

    // ==================== DTOs ====================

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class RegisterRequest
    {
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Rol { get; set; }
        public string Telefono { get; set; }
    }
}