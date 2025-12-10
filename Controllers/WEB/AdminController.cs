using Microsoft.AspNetCore.Mvc;
using VidaFit.Data;
using VidaFit.Services;

namespace VidaFit.Controllers.WEB
{
    [Route("admin")]
    public class AdminController : Controller  // ← CAMBIO AQUÍ: Controller, no ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;

        public AdminController(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // GET: /admin/login
        [HttpGet("login")]
        public IActionResult Login()
        {
            // Si ya está autenticado, redirigir al dashboard
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Dashboard");
            }

            return View();
        }

        // POST: /admin/login
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Email y contraseña son requeridos";
                return View();
            }

            try
            {
                // Usar el servicio de autenticación existente
                var result = await _authService.Login(email, password);

                if (!result.Success)
                {
                    ViewBag.Error = result.Message;
                    return View();
                }

                // Guardar datos en sesión
                HttpContext.Session.SetString("UserId", result.Usuario.Id.ToString());
                HttpContext.Session.SetString("UserName", result.Usuario.Nombre);
                HttpContext.Session.SetString("UserEmail", result.Usuario.Email);
                HttpContext.Session.SetString("UserRole", result.Usuario.Rol);

                // Redirigir al dashboard
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al iniciar sesión. Intenta nuevamente.";
                Console.WriteLine($"Error en login: {ex.Message}");
                return View();
            }
        }

        // GET: /admin/dashboard
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            // Verificar autenticación
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            // Pasar datos del usuario a la vista
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");

            return View("Index"); // Renderiza Views/Admin/Index.cshtml
        }

        // GET: /admin (redirige al dashboard)
        [HttpGet("")]
        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        // POST: /admin/logout
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: /admin/logout (también permitir GET)
        [HttpGet("logout")]
        public IActionResult LogoutGet()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}