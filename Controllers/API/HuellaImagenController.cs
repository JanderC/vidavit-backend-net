using Microsoft.AspNetCore.Mvc;
using VidaFitBackend.Services;

namespace VidaFit.Controllers.API
{
    /// <summary>
    /// Controlador de huellas basado en IMÁGENES
    /// Simple, confiable y funciona igual que el SDK de ejemplo
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HuellaImagenController : ControllerBase
    {
        private readonly IFingerprintImageService _fingerprintService;

        // Base de datos en memoria: Nombre -> Lista de imágenes Base64
        private static Dictionary<string, PersonaImagen> _memoriaPrueba = new();
        private static List<string> _logsPrueba = new();

        public HuellaImagenController(IFingerprintImageService fingerprintService)
        {
            _fingerprintService = fingerprintService;
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. VERIFICAR ESTADO DEL LECTOR
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var connected = _fingerprintService.IsReaderConnected();
            var readerInfo = _fingerprintService.GetReaderInfo();

            return Ok(new
            {
                success = true,
                connected,
                reader = readerInfo,
                personsInMemory = _memoriaPrueba.Count,
                mensaje = connected
                    ? "✅ Lector listo - Modo IMÁGENES (como SDK de ejemplo)"
                    : "❌ Lector no disponible"
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. CAPTURAR UNA IMAGEN
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("capturar")]
        public async Task<IActionResult> Capturar()
        {
            Log("═══════════════════════════════════════════");
            Log("📸 CAPTURA DE IMAGEN");
            Log("═══════════════════════════════════════════");

            var result = await _fingerprintService.CaptureImageAsync();

            if (!result.Success)
            {
                Log($"❌ Error: {result.Error}");
                return Ok(new
                {
                    success = false,
                    error = result.Error,
                    logs = result.Logs.Concat(GetLogs()).ToList()
                });
            }

            Log($"✅ Imagen capturada");
            Log($"   Dimensiones: {result.ImageWidth}x{result.ImageHeight}");
            Log($"   Tamaño: {result.ImageSize:N0} bytes");

            return Ok(new
            {
                success = true,
                mensaje = "✅ Imagen capturada correctamente",
                data = new
                {
                    imageBase64 = result.ImageBase64,
                    width = result.ImageWidth,
                    height = result.ImageHeight,
                    size = result.ImageSize
                },
                logs = result.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. REGISTRAR PERSONA (3 IMÁGENES)
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] RegistrarRequest request)
        {
            Log("═══════════════════════════════════════════");
            Log($"📝 REGISTRO: {request.Nombre}");
            Log("═══════════════════════════════════════════");

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Ok(new { success = false, error = "El nombre es requerido" });
            }

            string nombreKey = request.Nombre.ToLower().Trim();

            if (_memoriaPrueba.ContainsKey(nombreKey))
            {
                Log($"⚠️ {request.Nombre} ya está registrado");
                return Ok(new
                {
                    success = false,
                    error = $"{request.Nombre} ya está registrado. Usa /limpiar primero o elige otro nombre."
                });
            }

            Log("📸 Capturando 3 imágenes de tu huella...");
            Log("👆 Usa el MISMO dedo en las 3 capturas");

            var result = await _fingerprintService.CaptureMultipleImagesAsync(3);

            if (!result.Success)
            {
                Log($"❌ Error: {result.Error}");
                return Ok(new
                {
                    success = false,
                    error = result.Error,
                    capturasCompletadas = result.CapturesCompleted,
                    logs = result.Logs.Concat(GetLogs()).ToList()
                });
            }

            _memoriaPrueba[nombreKey] = new PersonaImagen
            {
                Nombre = request.Nombre,
                FechaRegistro = DateTime.UtcNow,
                ImagenesBase64 = result.ImagesBase64,
                NumImagenes = result.ImagesBase64.Count
            };

            Log($"✅ {request.Nombre} registrado exitosamente");
            Log($"   Imágenes: {result.CapturesCompleted}");

            return Ok(new
            {
                success = true,
                mensaje = $"✅ {request.Nombre} registrado con {result.CapturesCompleted} imágenes de huella",
                data = new
                {
                    nombre = request.Nombre,
                    capturas = result.CapturesCompleted,
                    primeraImagenBase64 = result.ImagesBase64.FirstOrDefault() // Para preview
                },
                logs = result.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. VERIFICAR (1:1) - Comparar con una persona específica
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("verificar")]
        public async Task<IActionResult> Verificar([FromBody] VerificarRequest request)
        {
            Log("═══════════════════════════════════════════");
            Log($"🔍 VERIFICACIÓN: {request.Nombre}");
            Log("═══════════════════════════════════════════");

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Ok(new { success = false, error = "El nombre es requerido" });
            }

            string nombreKey = request.Nombre.ToLower().Trim();

            if (!_memoriaPrueba.ContainsKey(nombreKey))
            {
                return Ok(new
                {
                    success = false,
                    error = $"{request.Nombre} no está registrado. Usa /registrar primero."
                });
            }

            Log($"📸 Capturando huella para verificar...");
            var captureResult = await _fingerprintService.CaptureImageAsync();

            if (!captureResult.Success)
            {
                return Ok(new
                {
                    success = false,
                    error = captureResult.Error,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }

            var persona = _memoriaPrueba[nombreKey];

            Log($"🔍 Comparando con {persona.NumImagenes} imágenes de {persona.Nombre}...");

            var (matched, bestSimilarity) = _fingerprintService.CompareAgainstMultiple(
                captureResult.ImageBase64,
                persona.ImagenesBase64
            );

            if (matched)
            {
                Log($"✅ ES {persona.Nombre}");
                Log($"   Similitud: {bestSimilarity:F2}% (umbral: 70%)");
            }
            else
            {
                Log($"❌ NO ES {persona.Nombre}");
                Log($"   Similitud: {bestSimilarity:F2}% (umbral: 70%)");
            }

            return Ok(new
            {
                success = true,
                matched,
                persona = persona.Nombre,
                similarity = Math.Round(bestSimilarity, 2),
                threshold = 70.0,
                capturedImageBase64 = captureResult.ImageBase64, // Para mostrar en UI
                mensaje = matched
                    ? $"✅ Verificado: ES {persona.Nombre}"
                    : $"❌ NO es {persona.Nombre}",
                interpretacion = bestSimilarity >= 90 ? "Coincidencia excelente" :
                                bestSimilarity >= 80 ? "Coincidencia muy buena" :
                                bestSimilarity >= 70 ? "Coincidencia aceptable" :
                                bestSimilarity >= 60 ? "Coincidencia dudosa" : "No coincide",
                logs = captureResult.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. IDENTIFICAR (1:N) - Buscar entre TODAS las personas
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("identificar")]
        public async Task<IActionResult> Identificar()
        {
            Log("═══════════════════════════════════════════");
            Log("🔍 IDENTIFICACIÓN 1:N (BUSCAR ENTRE TODOS)");
            Log("═══════════════════════════════════════════");

            if (_memoriaPrueba.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    error = "No hay personas registradas. Usa /registrar primero."
                });
            }

            Log($"📋 Personas registradas: {_memoriaPrueba.Count}");
            Log("📸 Capturando huella...");

            var captureResult = await _fingerprintService.CaptureImageAsync();

            if (!captureResult.Success)
            {
                return Ok(new
                {
                    success = false,
                    error = captureResult.Error,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }

            Log($"🔍 Buscando entre {_memoriaPrueba.Count} personas...");

            string? personaEncontrada = null;
            double mejorSimilitud = 0;

            foreach (var persona in _memoriaPrueba.Values)
            {
                var (matched, similarity) = _fingerprintService.CompareAgainstMultiple(
                    captureResult.ImageBase64,
                    persona.ImagenesBase64
                );

                Log($"   {persona.Nombre}: {similarity:F2}%");

                if (similarity > mejorSimilitud)
                {
                    mejorSimilitud = similarity;
                    if (matched)
                    {
                        personaEncontrada = persona.Nombre;
                    }
                }
            }

            if (personaEncontrada != null)
            {
                Log($"✅ IDENTIFICADO: {personaEncontrada}");
                Log($"   Similitud: {mejorSimilitud:F2}%");

                return Ok(new
                {
                    success = true,
                    matched = true,
                    persona = personaEncontrada,
                    similarity = Math.Round(mejorSimilitud, 2),
                    capturedImageBase64 = captureResult.ImageBase64, // Para mostrar en UI
                    totalBuscados = _memoriaPrueba.Count,
                    mensaje = $"✅ Identificado como {personaEncontrada}",
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }
            else
            {
                Log($"❌ NO IDENTIFICADO");
                Log($"   Mejor similitud: {mejorSimilitud:F2}% (umbral: 70%)");

                return Ok(new
                {
                    success = true,
                    matched = false,
                    bestSimilarity = Math.Round(mejorSimilitud, 2),
                    capturedImageBase64 = captureResult.ImageBase64, // Para mostrar en UI
                    mensaje = "❌ No se encontró coincidencia con ninguna persona registrada",
                    totalBuscados = _memoriaPrueba.Count,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. VER IMAGEN DE UNA PERSONA REGISTRADA
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("ver-imagen/{nombre}")]
        public IActionResult VerImagen(string nombre)
        {
            string nombreKey = nombre.ToLower().Trim();

            if (!_memoriaPrueba.ContainsKey(nombreKey))
            {
                return Ok(new
                {
                    success = false,
                    error = $"{nombre} no está registrado"
                });
            }

            var persona = _memoriaPrueba[nombreKey];

            return Ok(new
            {
                success = true,
                persona = persona.Nombre,
                numImagenes = persona.NumImagenes,
                primeraImagen = persona.ImagenesBase64.FirstOrDefault(),
                todasLasImagenes = persona.ImagenesBase64,
                fechaRegistro = persona.FechaRegistro
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // UTILIDADES
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("lista")]
        public IActionResult Lista()
        {
            var lista = _memoriaPrueba.Values.Select(p => new
            {
                nombre = p.Nombre,
                fechaRegistro = p.FechaRegistro,
                numImagenes = p.NumImagenes
            }).OrderBy(p => p.nombre).ToList();

            return Ok(new
            {
                success = true,
                total = lista.Count,
                personas = lista,
                mensaje = lista.Count == 0
                    ? "No hay personas registradas"
                    : $"{lista.Count} persona(s) registrada(s)"
            });
        }

        [HttpPost("limpiar")]
        public IActionResult Limpiar()
        {
            int count = _memoriaPrueba.Count;
            _memoriaPrueba.Clear();
            _logsPrueba.Clear();

            Log($"🗑️ Memoria limpiada: {count} registro(s) eliminados");

            return Ok(new
            {
                success = true,
                mensaje = $"✅ Se eliminaron {count} registro(s) de la memoria",
                registrosEliminados = count
            });
        }

        [HttpGet("logs")]
        public IActionResult GetLogsEndpoint()
        {
            return Ok(new
            {
                success = true,
                logs = GetLogs(100)
            });
        }

        private static void Log(string mensaje)
        {
            var logMensaje = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            _logsPrueba.Add(logMensaje);
            Console.WriteLine(logMensaje);

            if (_logsPrueba.Count > 500)
            {
                _logsPrueba.RemoveRange(0, 250);
            }
        }

        private static List<string> GetLogs(int count = 30)
        {
            return _logsPrueba.TakeLast(count).ToList();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // MODELOS
    // ════════════════════════════════════════════════════════════════════
    public class PersonaImagen
    {
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public List<string> ImagenesBase64 { get; set; } = new();
        public int NumImagenes { get; set; }
    }

    public class RegistrarRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public class VerificarRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }
}