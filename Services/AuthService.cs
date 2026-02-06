using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Services
{
    // ==================== INTERFACE ====================

    /// <summary>
    /// Interfaz para el servicio de autenticación
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Autentica un usuario con credenciales
        /// </summary>
        Task<AuthResult> Login(string email, string password);

        /// <summary>
        /// Obtiene un usuario por su email
        /// </summary>
        Task<Usuario> GetUsuarioByEmail(string email);

        /// <summary>
        /// Genera un token JWT para el usuario
        /// </summary>
        string GenerateJwtToken(Usuario usuario);

        /// <summary>
        /// Verifica si una contraseña es correcta
        /// </summary>
        bool VerifyPassword(string password, string hashedPassword);

        /// <summary>
        /// Genera un hash seguro de la contraseña
        /// </summary>
        string HashPassword(string password);
    }

    // ==================== IMPLEMENTATION ====================

    /// <summary>
    /// Servicio de autenticación con JWT y BCrypt
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Autentica un usuario con email y contraseña
        /// </summary>
        /// <param name="email">Email del usuario</param>
        /// <param name="password">Contraseña en texto plano</param>
        /// <returns>Resultado de autenticación con token JWT si es exitoso</returns>
        public async Task<AuthResult> Login(string email, string password)
        {
            try
            {
                Console.WriteLine($"Intento de login para: {email}");

                // Validar entrada
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Email y contraseña son requeridos"
                    };
                }

                // Buscar usuario por email (case-insensitive)
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                // Usuario no existe
                if (usuario == null)
                {
                    Console.WriteLine($"✗ Usuario no encontrado: {email}");
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Usuario no encontrado"
                    };
                }

                // Usuario inactivo
                if (!usuario.Activo)
                {
                    Console.WriteLine($"✗ Usuario inactivo: {email}");
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Usuario inactivo. Contacta al administrador."
                    };
                }

                // Verificar contraseña
                bool passwordValido = VerifyPassword(password, usuario.PasswordHash);

                if (!passwordValido)
                {
                    Console.WriteLine($"✗ Contraseña incorrecta para: {email}");
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Contraseña incorrecta"
                    };
                }

                // Login exitoso - generar token JWT
                var token = GenerateJwtToken(usuario);

                Console.WriteLine($"✓ Login exitoso para: {email} ({usuario.Rol})");

                return new AuthResult
                {
                    Success = true,
                    Token = token,
                    Message = "Autenticación exitosa",
                    Usuario = new UsuarioDto
                    {
                        Id = usuario.Id,
                        Nombre = usuario.Nombre,
                        Email = usuario.Email,
                        Rol = usuario.Rol,
                        Telefono = usuario.Telefono
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error en login: {ex.Message}");
                return new AuthResult
                {
                    Success = false,
                    Message = $"Error en autenticación: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Obtiene un usuario por su email
        /// </summary>
        /// <param name="email">Email del usuario</param>
        /// <returns>Usuario encontrado o null</returns>
        public async Task<Usuario> GetUsuarioByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        /// <summary>
        /// Genera un token JWT para el usuario autenticado
        /// </summary>
        /// <param name="usuario">Usuario autenticado</param>
        /// <returns>Token JWT como string</returns>
        public string GenerateJwtToken(Usuario usuario)
        {
            try
            {
                // Obtener configuración JWT
                var jwtKey = _configuration["Jwt:Key"] ?? "VidaFit2024SecretKey_MinimumLength32Chars!";
                var issuer = _configuration["Jwt:Issuer"] ?? "VidaFitAPI";
                var audience = _configuration["Jwt:Audience"] ?? "VidaFitClients";

                var key = Encoding.ASCII.GetBytes(jwtKey);

                // Días de expiración (por defecto 7)
                var expirationDays = 7;
                if (int.TryParse(_configuration["Jwt:ExpirationDays"], out int configDays))
                {
                    expirationDays = configDays;
                }

                // Crear claims del usuario
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Email, usuario.Email),
                    new Claim(ClaimTypes.Name, usuario.Nombre),
                    new Claim(ClaimTypes.Role, usuario.Rol),
                    new Claim("UserId", usuario.Id.ToString()),
                    new Claim("UserName", usuario.Nombre),
                    new Claim("UserEmail", usuario.Email),
                    new Claim(JwtRegisteredClaimNames.Sub, usuario.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                };

                // Crear descriptor del token
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.Now.AddDays(expirationDays),
                    Issuer = issuer,
                    Audience = audience,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };

                // Generar token
                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                Console.WriteLine($"✓ Token JWT generado para: {usuario.Email}");
                Console.WriteLine($"  Expira: {tokenDescriptor.Expires:dd/MM/yyyy HH:mm:ss} UTC");
                Console.WriteLine($"  Rol: {usuario.Rol}");

                return tokenString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error generando token JWT: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verifica si una contraseña coincide con el hash almacenado
        /// Soporta tanto BCrypt (producción) como comparación directa (desarrollo)
        /// </summary>
        /// <param name="password">Contraseña en texto plano</param>
        /// <param name="hashedPassword">Hash almacenado en la base de datos</param>
        /// <returns>True si la contraseña es correcta</returns>
        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
            {
                return false;
            }

            try
            {
                // OPCIÓN 1: Si el hash tiene formato BCrypt (empieza con $2a$, $2b$, $2y$)
                if (hashedPassword.StartsWith("$2"))
                {
                    try
                    {
                        bool isValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                        Console.WriteLine($"Verificación BCrypt: {(isValid ? "✓" : "✗")}");
                        return isValid;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error en verificación BCrypt: {ex.Message}");
                        // Si falla BCrypt, intentar comparación directa como fallback
                    }
                }

                // OPCIÓN 2: Comparación directa (SOLO PARA DESARROLLO)
                // Esto permite que funcione con la contraseña por defecto "admin123"
                // que se inserta sin hash en el script SQL inicial
                bool directMatch = password == hashedPassword;

                if (directMatch)
                {
                    Console.WriteLine("⚠️  ADVERTENCIA: Usando comparación directa de contraseña");
                    Console.WriteLine("   Para producción, actualiza las contraseñas a BCrypt");
                }

                return directMatch;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error verificando contraseña: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Genera un hash seguro de la contraseña usando BCrypt
        /// </summary>
        /// <param name="password">Contraseña en texto plano</param>
        /// <returns>Hash BCrypt de la contraseña</returns>
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("La contraseña no puede estar vacía");
            }

            try
            {
                // Genera un hash BCrypt con factor de trabajo 12
                // Factor 12 = balance entre seguridad (2^12 = 4096 iteraciones) y rendimiento
                // Opciones: 10 (más rápido), 12 (recomendado), 14 (más seguro)
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

                Console.WriteLine($"✓ Hash BCrypt generado (longitud: {hashedPassword.Length})");

                return hashedPassword;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error generando hash: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Actualiza la contraseña de un usuario (con hash BCrypt)
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="newPassword">Nueva contraseña en texto plano</param>
        /// <returns>True si se actualizó exitosamente</returns>
        public async Task<bool> UpdatePassword(Guid userId, string newPassword)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(userId);

                if (usuario == null)
                {
                    Console.WriteLine($"✗ Usuario no encontrado: {userId}");
                    return false;
                }

                // Generar hash BCrypt de la nueva contraseña
                usuario.PasswordHash = HashPassword(newPassword);
                usuario.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                Console.WriteLine($"✓ Contraseña actualizada para: {usuario.Email}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error actualizando contraseña: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Valida el formato de un email
        /// </summary>
        /// <param name="email">Email a validar</param>
        /// <returns>True si el formato es válido</returns>
        public bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // ==================== DTOs (Data Transfer Objects) ====================

    /// <summary>
    /// Resultado de una operación de autenticación
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        /// Indica si la autenticación fue exitosa
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Token JWT si la autenticación fue exitosa
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Información del usuario autenticado
        /// </summary>
        public UsuarioDto Usuario { get; set; }
    }

    /// <summary>
    /// DTO para transferir datos de usuario sin información sensible
    /// </summary>
    public class UsuarioDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public string Telefono { get; set; }
    }
}