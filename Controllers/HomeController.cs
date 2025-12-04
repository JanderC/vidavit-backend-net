using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VidaFitBackend.Services;
using VidaFit.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace VidaFit.Controllers.Web
{
    // ==================== HOME CONTROLLER ====================
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Redirigir al kiosko por defecto
            return RedirectToAction("Index", "Kiosko");
        }
    }

    // ==================== KIOSKO CONTROLLER ====================
    public class KioskoController : Controller
    {
        private readonly IFingerprintService _fingerprintService;

        public KioskoController(IFingerprintService fingerprintService)
        {
            _fingerprintService = fingerprintService;
        }

        public IActionResult Index()
        {
            ViewBag.ReaderConnected = _fingerprintService.IsReaderConnected();
            return View();
        }
    }

    // ==================== ADMIN CONTROLLER ====================
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Verificar si hay sesión activa
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Validación simple (mejorar con IAuthService)
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == email && u.Activo);

            if (usuario != null && password == usuario.PasswordHash) // Temporal - usar BCrypt
            {
                HttpContext.Session.SetString("UserId", usuario.Id.ToString());
                HttpContext.Session.SetString("UserName", usuario.Nombre);
                HttpContext.Session.SetString("UserRole", usuario.Rol);

                return RedirectToAction("Index");
            }

            ViewBag.Error = "Credenciales incorrectas";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }

    // ==================== CLIENTES CONTROLLER ====================
    public class ClientesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFingerprintService _fingerprintService;

        public ClientesController(AppDbContext context, IFingerprintService fingerprintService)
        {
            _context = context;
            _fingerprintService = fingerprintService;
        }

        public async Task<IActionResult> Index()
        {
            // Verificar sesión
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var clientes = await _context.Clientes
                .Where(c => c.Activo)
                .OrderBy(c => c.Apellido)
                .ThenBy(c => c.Nombre)
                .ToListAsync();

            return View(clientes);
        }

        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            return View();
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            return View(cliente);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var cliente = await _context.Clientes
                .Include(c => c.Membresias)
                    .ThenInclude(m => m.Plan)
                .Include(c => c.CheckIns)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
                return NotFound();

            return View(cliente);
        }

        public IActionResult CaptureFingerprint(Guid id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            ViewBag.ClienteId = id;
            ViewBag.ReaderConnected = _fingerprintService.IsReaderConnected();
            return View();
        }
    }

    // ==================== MEMBRESIAS CONTROLLER ====================
    public class MembresiasController : Controller
    {
        private readonly AppDbContext _context;

        public MembresiasController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var membresias = await _context.Membresias
                .Include(m => m.Cliente)
                .Include(m => m.Plan)
                .OrderByDescending(m => m.FechaInicio)
                .ToListAsync();

            return View(membresias);
        }

        public async Task<IActionResult> Create()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            ViewBag.Clientes = await _context.Clientes.Where(c => c.Activo).ToListAsync();
            ViewBag.Planes = await _context.Planes.Where(p => p.Activo).ToListAsync();

            return View();
        }
    }

    // ==================== PRODUCTOS CONTROLLER ====================
    public class ProductosController : Controller
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var productos = await _context.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(productos);
        }

        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            return View();
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            return View(producto);
        }
    }

    // ==================== REPORTES CONTROLLER ====================
    public class ReportesController : Controller
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            return View();
        }

        public IActionResult Asistencia()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            return View();
        }

        public IActionResult Ingresos()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Admin");

            return View();
        }
    }
}