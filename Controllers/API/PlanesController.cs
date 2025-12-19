using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly string[] TIPOS_VALIDOS = { "semanal", "mensual", "personalizado", "diario", "trimestral", "semestral", "anual", "otro" };

        public PlanesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Plan>>> GetPlanes()
        {
            return await _context.Planes
                .OrderByDescending(p => p.Activo)
                .ThenBy(p => p.Nombre)
                .ToListAsync();
        }

        [HttpGet("activos")]
        public async Task<ActionResult<IEnumerable<Plan>>> GetPlanesActivos()
        {
            return await _context.Planes
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Plan>> GetPlan(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound();
            return plan;
        }

        [HttpGet("tipo/{tipo}")]
        public async Task<ActionResult<IEnumerable<Plan>>> GetPlanesPorTipo(string tipo)
        {
            if (!TIPOS_VALIDOS.Contains(tipo.ToLower()))
                return BadRequest($"Tipo inválido. Permitidos: {string.Join(", ", TIPOS_VALIDOS)}");

            return await _context.Planes
                .Where(p => p.Tipo == tipo.ToLower() && p.Activo)
                .OrderBy(p => p.Precio)
                .ToListAsync();
        }

        public class PlanDto
        {
            public string Nombre { get; set; }
            public string? Descripcion { get; set; }
            public string Tipo { get; set; }
            public int DuracionDias { get; set; }
            public decimal Precio { get; set; }
            public string? Color { get; set; }
            public bool Activo { get; set; } = true;
        }

        [HttpPost]
        public async Task<ActionResult<Plan>> CreatePlan([FromBody] PlanDto planDto)
        {
            if (string.IsNullOrWhiteSpace(planDto.Nombre))
                return BadRequest("El nombre del plan es requerido");

            if (string.IsNullOrWhiteSpace(planDto.Tipo))
                return BadRequest("El tipo de plan es requerido");

            if (!TIPOS_VALIDOS.Contains(planDto.Tipo.ToLower()))
                return BadRequest($"Tipo inválido '{planDto.Tipo}'. Solo se permite: semanal, mensual, personalizado");

            if (planDto.DuracionDias <= 0)
                return BadRequest("La duración debe ser mayor a 0");

            if (planDto.Precio <= 0)
                return BadRequest("El precio debe ser mayor a 0");

            var existeNombre = await _context.Planes
                .AnyAsync(p => p.Nombre.ToLower() == planDto.Nombre.ToLower());

            if (existeNombre)
                return BadRequest("Ya existe un plan con ese nombre");

            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = planDto.Nombre,
                Descripcion = planDto.Descripcion,
                Tipo = planDto.Tipo.ToLower(),
                DuracionDias = planDto.DuracionDias,
                Precio = planDto.Precio,
                Color = string.IsNullOrWhiteSpace(planDto.Color) ? "#00FF00" : planDto.Color,
                Activo = planDto.Activo,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Planes.Add(plan);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] Plan plan)
        {
            if (id != plan.Id)
                return BadRequest("El ID del plan no coincide");

            var dbPlan = await _context.Planes.FindAsync(id);
            if (dbPlan == null)
                return NotFound("Plan no encontrado");

            if (string.IsNullOrWhiteSpace(plan.Nombre))
                return BadRequest("El nombre del plan es requerido");

            if (string.IsNullOrWhiteSpace(plan.Tipo))
                return BadRequest("El tipo de plan es requerido");

            if (!TIPOS_VALIDOS.Contains(plan.Tipo.ToLower()))
                return BadRequest($"Tipo inválido '{plan.Tipo}'. Solo se permite: semanal, mensual, personalizado");

            if (plan.DuracionDias <= 0)
                return BadRequest("La duración debe ser mayor a 0");

            if (plan.Precio <= 0)
                return BadRequest("El precio debe ser mayor a 0");

            var existeNombre = await _context.Planes
                .AnyAsync(p => p.Nombre.ToLower() == plan.Nombre.ToLower() && p.Id != id);

            if (existeNombre)
                return BadRequest("Ya existe otro plan con ese nombre");

            dbPlan.Nombre = plan.Nombre;
            dbPlan.Descripcion = plan.Descripcion;
            dbPlan.Tipo = plan.Tipo.ToLower();
            dbPlan.DuracionDias = plan.DuracionDias;
            dbPlan.Precio = plan.Precio;
            dbPlan.Color = plan.Color;
            dbPlan.Activo = plan.Activo;
            dbPlan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound("Plan no encontrado");

            var tieneMembresias = await _context.Membresias.AnyAsync(m => m.PlanId == id);

            if (tieneMembresias)
            {
                return BadRequest(
                    "No se puede eliminar el plan porque tiene membresías asociadas. " +
                    "Puede desactivarlo en su lugar."
                );
            }

            _context.Planes.Remove(plan);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Plan eliminado exitosamente" });
        }

        [HttpPatch("{id}/toggle-activo")]
        public async Task<IActionResult> ToggleActivo(Guid id)
        {
            var plan = await _context.Planes.FindAsync(id);
            if (plan == null)
                return NotFound("Plan no encontrado");

            plan.Activo = !plan.Activo;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Plan {(plan.Activo ? "activado" : "desactivado")} exitosamente",
                activo = plan.Activo
            });
        }

        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticas()
        {
            var totalPlanes = await _context.Planes.CountAsync();
            var planesActivos = await _context.Planes.CountAsync(p => p.Activo);

            var membresiasActivas = await _context.Membresias
                .Where(m => m.Estado == "activa")
                .GroupBy(m => m.PlanId)
                .Select(g => new
                {
                    PlanId = g.Key,
                    Cantidad = g.Count()
                })
                .ToListAsync();

            var planesMasVendidos = await _context.Membresias
                .Include(m => m.Plan)
                .GroupBy(m => m.PlanId)
                .Select(g => new
                {
                    Plan = g.First().Plan,
                    TotalVentas = g.Count(),
                    IngresoTotal = g.Sum(m => m.MontoPagado)
                })
                .OrderByDescending(x => x.TotalVentas)
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                totalPlanes,
                planesActivos,
                membresiasActivas,
                planesMasVendidos
            });
        }

        [HttpGet("tipos")]
        public ActionResult<IEnumerable<object>> GetTipos()
        {
            var tipos = TIPOS_VALIDOS.Select(t => new
            {
                valor = t,
                nombre = char.ToUpper(t[0]) + t.Substring(1)
            });

            return Ok(tipos);
        }
    }
}