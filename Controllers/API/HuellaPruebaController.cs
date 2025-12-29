using Microsoft.AspNetCore.Mvc;
using VidaFitBackend.Services;
using DPUruNet;

namespace VidaFit.Controllers.API
{
    /// <summary>
    /// Controlador de PRUEBA para el lector de huellas DigitalPersona 4500
    /// USA MÉTODO CORRECTO: CreateFmdFromFid
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HuellaPruebaController : ControllerBase
    {
        private readonly IFingerprintServiceTest _fingerprintService;
        private static Dictionary<string, PersonaPrueba> _memoriaPrueba = new();
        private static List<string> _logsPrueba = new();

        public HuellaPruebaController(IFingerprintServiceTest fingerprintService)
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
                    ? "✅ Lector listo con método correcto (CreateFmdFromFid)"
                    : "❌ Lector no disponible"
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. DIAGNÓSTICO COMPLETO
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("diagnostico")]
        public async Task<IActionResult> Diagnostico()
        {
            Log("═══════════════════════════════════════════");
            Log("🔍 DIAGNÓSTICO CON MÉTODO CORRECTO");
            Log("═══════════════════════════════════════════");

            var diagnostico = await _fingerprintService.DiagnosticarCaptura();

            var exitosos = diagnostico.Resultados.Where(r => r.Exitoso).ToList();

            if (exitosos.Any())
            {
                Log($"✅ FUNCIONANDO CORRECTAMENTE");
                Log($"   Método: {exitosos[0].Metodo}");
                Log($"   FMD: {exitosos[0].TamanoFmd:N0} bytes");
            }
            else
            {
                Log($"❌ FALLÓ - Revisa los detalles");
            }

            return Ok(new
            {
                success = true,
                lectorConectado = diagnostico.LectorConectado,
                infoLector = diagnostico.InfoLector,
                resultados = diagnostico.Resultados.Select(r => new
                {
                    metodo = r.Metodo,
                    formato = r.Formato,
                    exitoso = r.Exitoso,
                    tamanoFid = r.TamanoFid,
                    tamanoFmd = r.TamanoFmd,
                    error = r.Error,
                    detallesCount = r.Detalles.Count,
                    ultimosDetalles = r.Detalles.TakeLast(10).ToList()
                }).ToList(),
                resumen = new
                {
                    funciona = exitosos.Any(),
                    mensaje = exitosos.Any()
                        ? "✅ Todo OK - Puedes registrar y verificar huellas"
                        : "❌ Problema detectado - Revisa los logs"
                },
                logs = GetLogs()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. CAPTURA SIMPLE
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("capturar")]
        public async Task<IActionResult> Capturar()
        {
            Log("═══════════════════════════════════════════");
            Log("CAPTURA DE HUELLA");
            Log("═══════════════════════════════════════════");

            var result = await _fingerprintService.CaptureFmdAsync();

            if (!result.Success)
            {
                Log($"❌ Error: {result.Error}");
                return Ok(new
                {
                    success = false,
                    error = result.Error,
                    logs = result.Logs.Concat(GetLogs()).ToList(),
                    ayuda = result.Error.Contains("TOO_SMALL")
                        ? "Cubre COMPLETAMENTE el sensor con tu dedo y presiona firmemente"
                        : "Revisa los logs para más detalles"
                });
            }

            Log($"✅ Captura exitosa");
            Log($"   FMD: {result.FmdSize:N0} bytes");
            Log($"   RAW: {result.RawImageSize:N0} bytes");

            return Ok(new
            {
                success = true,
                mensaje = "Huella capturada correctamente",
                data = new
                {
                    fmdSize = result.FmdSize,
                    rawSize = result.RawImageSize,
                    compresion = $"{((double)result.RawImageSize / result.FmdSize):F1}x",
                    formato = result.Formato
                },
                logs = result.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. REGISTRAR PERSONA (3 CAPTURAS)
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] RegistrarHuellaRequest request)
        {
            Log("═══════════════════════════════════════════");
            Log($"REGISTRO: {request.Nombre}");
            Log("═══════════════════════════════════════════");

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Ok(new { success = false, error = "El nombre es requerido" });
            }

            if (_memoriaPrueba.ContainsKey(request.Nombre.ToLower()))
            {
                Log($"⚠️ {request.Nombre} ya está registrado");
                return Ok(new
                {
                    success = false,
                    error = $"{request.Nombre} ya está registrado. Usa /limpiar primero o elige otro nombre."
                });
            }

            var result = await _fingerprintService.CaptureMultipleFmdsAsync(3);

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

            _memoriaPrueba[request.Nombre.ToLower()] = new PersonaPrueba
            {
                Nombre = request.Nombre,
                FechaRegistro = DateTime.UtcNow,
                Fmds = result.Fmds,
                TamanoTotal = result.TotalFmdSize
            };

            Log($"✅ {request.Nombre} registrado exitosamente");
            Log($"   Capturas: {result.CapturesCompleted}");
            Log($"   Tamaño total: {result.TotalFmdSize:N0} bytes");

            return Ok(new
            {
                success = true,
                mensaje = $"✅ {request.Nombre} registrado con {result.CapturesCompleted} huellas",
                data = new
                {
                    nombre = request.Nombre,
                    capturas = result.CapturesCompleted,
                    tamanoTotal = result.TotalFmdSize,
                    promedioBytes = result.TotalFmdSize / result.CapturesCompleted
                },
                logs = result.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. VERIFICAR (1:1) - Verificar si es una persona específica
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("verificar")]
        public async Task<IActionResult> Verificar([FromBody] VerificarHuellaRequest request)
        {
            Log("═══════════════════════════════════════════");
            Log($"VERIFICACIÓN 1:1 contra: {request.Nombre}");
            Log("═══════════════════════════════════════════");

            if (!_memoriaPrueba.ContainsKey(request.Nombre.ToLower()))
            {
                return Ok(new
                {
                    success = false,
                    error = $"{request.Nombre} no está registrado. Usa /registrar primero."
                });
            }

            Log("📸 Capturando huella para verificar...");
            var captureResult = await _fingerprintService.CaptureFmdAsync();

            if (!captureResult.Success)
            {
                return Ok(new
                {
                    success = false,
                    error = captureResult.Error,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }

            var persona = _memoriaPrueba[request.Nombre.ToLower()];
            var (matched, bestScore) = _fingerprintService.CompareFmdAgainstMultiple(
                captureResult.Fmd,
                persona.Fmds
            );

            if (matched)
            {
                Log($"✅ ES {persona.Nombre}");
                Log($"   Score: {bestScore:N0} (umbral: 20,000)");
            }
            else
            {
                Log($"❌ NO ES {persona.Nombre}");
                Log($"   Score: {bestScore:N0} (umbral: 20,000)");
            }

            return Ok(new
            {
                success = true,
                matched,
                persona = persona.Nombre,
                score = bestScore,
                umbral = 20000,
                mensaje = matched
                    ? $"✅ Verificado: ES {persona.Nombre}"
                    : $"❌ NO es {persona.Nombre}",
                interpretacion = bestScore < 10000 ? "Coincidencia excelente" :
                                 bestScore < 20000 ? "Coincidencia buena" :
                                 bestScore < 30000 ? "Coincidencia dudosa" : "No coincide",
                logs = captureResult.Logs.Concat(GetLogs()).ToList()
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. IDENTIFICAR (1:N) - Buscar entre TODAS las personas
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("identificar")]
        public async Task<IActionResult> Identificar()
        {
            Log("═══════════════════════════════════════════");
            Log("IDENTIFICACIÓN 1:N (BUSCAR ENTRE TODOS)");
            Log("═══════════════════════════════════════════");

            if (_memoriaPrueba.Count == 0)
            {
                return Ok(new { success = false, error = "No hay personas registradas. Usa /registrar primero." });
            }

            Log($"📋 Personas registradas: {_memoriaPrueba.Count}");
            Log("📸 Capturando huella...");

            var captureResult = await _fingerprintService.CaptureFmdAsync();

            if (!captureResult.Success)
            {
                return Ok(new
                {
                    success = false,
                    error = captureResult.Error,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }

            // Construir lista de todos los FMDs
            var allFmds = new List<Fmd>();
            var indexToPersonMap = new Dictionary<int, string>();
            int currentIndex = 0;

            foreach (var persona in _memoriaPrueba.Values)
            {
                foreach (var fmd in persona.Fmds)
                {
                    allFmds.Add(fmd);
                    indexToPersonMap[currentIndex] = persona.Nombre;
                    currentIndex++;
                }
            }

            Log($"🔍 Buscando en {allFmds.Count} huellas registradas...");

            var identifyResult = _fingerprintService.IdentifyFmdAgainstAll(
                captureResult.Fmd,
                allFmds,
                20000
            );

            if (identifyResult != null && identifyResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
            {
                int[][] indexes = identifyResult.Indexes;
                int matchIndex = indexes[0][0];
                string personaEncontrada = indexToPersonMap[matchIndex];

                Log($"✅ IDENTIFICADO: {personaEncontrada}");
                Log($"   Índice FMD: {matchIndex}");

                return Ok(new
                {
                    success = true,
                    matched = true,
                    persona = personaEncontrada,
                    matchIndex = matchIndex,
                    totalBuscados = allFmds.Count,
                    mensaje = $"✅ Identificado como {personaEncontrada}",
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }
            else
            {
                Log($"❌ NO IDENTIFICADO");
                Log($"   No hay coincidencias en la base de datos");

                return Ok(new
                {
                    success = true,
                    matched = false,
                    mensaje = "❌ No se encontró coincidencia con ninguna persona registrada",
                    totalBuscados = allFmds.Count,
                    logs = captureResult.Logs.Concat(GetLogs()).ToList()
                });
            }
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
                numHuellas = p.Fmds.Count,
                tamanoTotal = p.TamanoTotal
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
    public class PersonaPrueba
    {
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public List<Fmd> Fmds { get; set; } = new();
        public int TamanoTotal { get; set; }
    }

    public class RegistrarHuellaRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public class VerificarHuellaRequest
    {
        public string Nombre { get; set; } = string.Empty;
    }
}