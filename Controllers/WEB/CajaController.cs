using Microsoft.AspNetCore.Mvc;

namespace VidaFit.Controllers.WEB
{
    /// <summary>
    /// Controlador para las vistas de Caja
    /// </summary>
    public class CajaController : Controller
    {
        /// <summary>
        /// Vista principal de caja
        /// GET: /caja
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Redirecciona a la vista principal con tab de empleados
        /// GET: /caja/empleados
        /// </summary>
        public IActionResult Empleados()
        {
            ViewData["TabInicial"] = "empleados";
            return View("Index");
        }

        /// <summary>
        /// Redirecciona a la vista principal con tab de deudas
        /// GET: /caja/deudas
        /// </summary>
        public IActionResult Deudas()
        {
            ViewData["TabInicial"] = "deudas";
            return View("Index");
        }
    }
}