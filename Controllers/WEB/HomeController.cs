using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using System.Threading.Tasks;

namespace VidaFit.Controllers.WEB
{
    [Route("")]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Ejemplo: puedes cargar métricas para el dashboard principal
            var totalClientes = await _context.Clientes.CountAsync();
            var totalMembresias = await _context.Membresias.CountAsync();
            var checkInsHoy = await _context.CheckIns.CountAsync(c => c.FechaHora.Date == System.DateTime.UtcNow.Date);

            ViewBag.TotalClientes = totalClientes;
            ViewBag.TotalMembresias = totalMembresias;
            ViewBag.CheckInsHoy = checkInsHoy;

            return View();
        }

        // GET: /acerca
        [HttpGet("acerca")]
        public IActionResult Acerca()
        {
            return View();
        }
    }
}