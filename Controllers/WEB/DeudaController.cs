using Microsoft.AspNetCore.Mvc;

namespace VidaFit.Controllers.WEB
{
    /// <summary>
    /// Controlador WEB para las vistas de Deuda
    /// </summary>
    public class DeudaController : Controller
    {
        /// <summary>
        /// Vista de historial completo de una deuda específica
        /// GET: /deuda/historial/{id}
        /// </summary>
        public IActionResult Historial(Guid id)
        {
            if (id == Guid.Empty)
                return RedirectToAction("Index", "Caja");

            ViewData["DeudaId"] = id.ToString();
            return View();
        }
    }
}
