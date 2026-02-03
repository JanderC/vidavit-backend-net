using Microsoft.AspNetCore.Mvc;

namespace VidaFit.Controllers.WEB
{
    /// <summary>
    /// Controlador para las vistas de Caja Fuerte
    /// </summary>
    public class CajaFuerteController : Controller
    {
        private readonly ILogger<CajaFuerteController> _logger;

        public CajaFuerteController(ILogger<CajaFuerteController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Vista principal de Caja Fuerte
        /// GET: /cajafuerte
        /// </summary>
        public IActionResult Index()
        {
            _logger.LogInformation("Accediendo a Caja Fuerte");
            return View();
        }
    }
}