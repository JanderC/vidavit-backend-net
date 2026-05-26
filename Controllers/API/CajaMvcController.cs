using Microsoft.AspNetCore.Mvc;

namespace VidaFit.Controllers
{
    [Route("Caja")]
    public class CajaMvcController : Controller
    {
        [Route("")]
        [Route("Index")]
        public IActionResult Index()
        {
            return View("~/Views/Caja/Index.cshtml");
        }

        [Route("Historial")]
        public IActionResult Historial()
        {
            return View("~/Views/Caja/Historial.cshtml");
        }

        [Route("DeudasPagadas")]
        public IActionResult DeudasPagadas()
        {
            return View("~/Views/Caja/DeudasPagadas.cshtml");
        }
    }
}