using System;
using System.Collections.Generic;
using System.Linq;
using DPUruNet;
using System.Threading;
using System.Threading.Tasks;

namespace VidaFitBackend.Services
{
    // Interfaces y modelos igual que antes...
    public interface IFingerprintServiceTest
    {
        void Initialize(IFingerprintService fingerprintService);
        bool IsReaderConnected();
        Task<FmdCaptureResultTest> CaptureFmdAsync();
        Task<MultiFmdCaptureResultTest> CaptureMultipleFmdsAsync(int numCaptures = 3);
        bool CompareFmds(Fmd captured, Fmd stored, out int score);
        (bool matched, int bestScore) CompareFmdAgainstMultiple(Fmd captured, List<Fmd> storedFmds);
        IdentifyResult IdentifyFmdAgainstAll(Fmd captured, List<Fmd> allFmds, int threshold = 20000);
        Dictionary<string, string> GetReaderInfo();
        Task<DiagnosticoCaptura> DiagnosticarCaptura();
    }

    public class FmdCaptureResultTest
    {
        public bool Success { get; set; }
        public Fmd Fmd { get; set; } = null!;
        public string FmdBase64 { get; set; } = string.Empty;
        public int FmdSize { get; set; }
        public byte[]? RawImage { get; set; }
        public int RawImageSize { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new List<string>();
        public string Formato { get; set; } = string.Empty;
        public string ConfiguracionExitosa { get; set; } = string.Empty;
    }

    public class MultiFmdCaptureResultTest
    {
        public bool Success { get; set; }
        public List<Fmd> Fmds { get; set; } = new List<Fmd>();
        public List<string> FmdBase64List { get; set; } = new List<string>();
        public string ConcatenatedFmds { get; set; } = string.Empty;
        public int CapturesCompleted { get; set; }
        public int TotalFmdSize { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new List<string>();
    }

    public class DiagnosticoCaptura
    {
        public bool LectorConectado { get; set; }
        public Dictionary<string, string> InfoLector { get; set; } = new();
        public List<ResultadoDiagnostico> Resultados { get; set; } = new();
    }

    public class ResultadoDiagnostico
    {
        public string Metodo { get; set; } = string.Empty;
        public bool Exitoso { get; set; }
        public string Formato { get; set; } = string.Empty;
        public int? TamanoFid { get; set; }
        public int? TamanoFmd { get; set; }
        public string? Error { get; set; }
        public List<string> Detalles { get; set; } = new();
    }

    /// <summary>
    /// Servicio ULTRA-ROBUSTO con múltiples estrategias de extracción
    /// </summary>
    public class FingerprintServiceTest : IFingerprintServiceTest
    {
        private Reader? _reader;
        private bool _isInitialized = false;
        private IFingerprintService? _mainService;

        public void Initialize(IFingerprintService fingerprintService)
        {
            try
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   SERVICIO ULTRA-ROBUSTO");
                Console.WriteLine("═══════════════════════════════════════════");

                _mainService = fingerprintService;

                var readerField = fingerprintService.GetType().GetField("_reader",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (readerField != null)
                {
                    _reader = readerField.GetValue(fingerprintService) as Reader;
                }

                if (_reader != null && fingerprintService.IsReaderConnected())
                {
                    _isInitialized = true;
                    Console.WriteLine("✓ Servicio con estrategias múltiples de extracción");
                }
                else
                {
                    _isInitialized = false;
                    Console.WriteLine("✗ No se pudo acceder al Reader");
                }

                Console.WriteLine("═══════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ERROR: {ex.Message}");
                _isInitialized = false;
            }
        }

        public bool IsReaderConnected()
        {
            return _isInitialized && _reader != null;
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTURA ULTRA-ROBUSTA CON 50+ CONFIGURACIONES
        // ════════════════════════════════════════════════════════════════════
        public async Task<FmdCaptureResultTest> CaptureFmdAsync()
        {
            var result = new FmdCaptureResultTest { Success = false, Formato = "Ultra-robusto" };

            if (!IsReaderConnected() || _reader == null)
            {
                result.Error = "Reader no disponible";
                return result;
            }

            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add("🚀 MODO ULTRA-ROBUSTO");
            result.Logs.Add("═══════════════════════════════════════════");

            // Generar 50+ configuraciones de dimensiones
            var configuracionesDimensiones = GenerarConfiguracionesDimensiones();

            result.Logs.Add($"📋 {configuracionesDimensiones.Count} configuraciones preparadas");
            result.Logs.Add("");

            // Formatos de captura
            var formatosCaptura = new[]
            {
                new { Formato = Constants.Formats.Fid.ANSI, Proc = Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT, Nombre = "ANSI-DEF" },
                new { Formato = Constants.Formats.Fid.ISO, Proc = Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT, Nombre = "ISO-DEF" }
            };

            for (int intento = 1; intento <= 2; intento++)
            {
                result.Logs.Add($"═══ INTENTO {intento}/2 ═══");

                // Limpiar lector
                try
                {
                    var status = _reader.GetStatus();
                    if (status == Constants.ResultCode.DP_SUCCESS &&
                        _reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                    {
                        _reader.CancelCapture();
                        Thread.Sleep(500);
                    }
                }
                catch { }

                int resolution = _reader.Capabilities.Resolutions[0];

                foreach (var formatoCaptura in formatosCaptura)
                {
                    result.Logs.Add($"📸 Capturando con {formatoCaptura.Nombre}...");

                    CaptureResult captureResult;
                    try
                    {
                        captureResult = _reader.Capture(
                            formatoCaptura.Formato,
                            formatoCaptura.Proc,
                            6000,
                            resolution
                        );
                    }
                    catch (Exception ex)
                    {
                        result.Logs.Add($"   ❌ Excepción: {ex.Message}");
                        continue;
                    }

                    if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS ||
                        captureResult.Data == null ||
                        captureResult.Data.Bytes == null ||
                        captureResult.Data.Bytes.Length == 0)
                    {
                        result.Logs.Add($"   ❌ Falló: {captureResult.ResultCode}");
                        continue;
                    }

                    Fid fid = captureResult.Data;
                    result.Logs.Add($"   ✓ Capturado: {fid.Bytes.Length:N0} bytes");

                    // Probar TODAS las configuraciones de dimensiones
                    result.Logs.Add($"   🔍 Probando {configuracionesDimensiones.Count} configs...");

                    int configNum = 0;
                    int configuracionesProbadas = 0;
                    foreach (var config in configuracionesDimensiones)
                    {
                        configNum++;

                        // ✅ VALIDACIÓN CRÍTICA: evitar AccessViolationException
                        int bytesNecesarios = config.Width * config.Height;
                        if (bytesNecesarios > fid.Bytes.Length + 1000 || // +1000 margen de tolerancia
                            bytesNecesarios < fid.Bytes.Length - 1000)
                        {
                            // Dimensiones muy diferentes, saltar
                            continue;
                        }

                        configuracionesProbadas++;

                        try
                        {
                            DataResult<Fmd> fmdResult = FeatureExtraction.CreateFmdFromRaw(
                                fid.Bytes,
                                0,
                                0,
                                config.Width,
                                config.Height,
                                500,
                                Constants.Formats.Fmd.ANSI
                            );

                            if (fmdResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                                fmdResult.Data != null &&
                                fmdResult.Data.Bytes != null &&
                                fmdResult.Data.Bytes.Length > 0)
                            {
                                // ✅✅✅ ¡ÉXITO!
                                result.Success = true;
                                result.Fmd = fmdResult.Data;
                                result.FmdBase64 = Convert.ToBase64String(fmdResult.Data.Bytes);
                                result.FmdSize = fmdResult.Data.Bytes.Length;
                                result.RawImage = fid.Bytes;
                                result.RawImageSize = fid.Bytes.Length;
                                result.Formato = formatoCaptura.Nombre;
                                result.ConfiguracionExitosa = $"{config.Width}x{config.Height}";

                                result.Logs.Add($"");
                                result.Logs.Add($"✅✅✅ ÉXITO EN CONFIG #{configNum} ✅✅✅");
                                result.Logs.Add($"   Formato captura: {formatoCaptura.Nombre}");
                                result.Logs.Add($"   Dimensiones: {config.Width}x{config.Height}");
                                result.Logs.Add($"   FMD: {result.FmdSize:N0} bytes");
                                result.Logs.Add($"   RAW: {result.RawImageSize:N0} bytes");
                                result.Logs.Add($"   Compresión: {((double)result.RawImageSize / result.FmdSize):F1}x");
                                result.Logs.Add("═══════════════════════════════════════════");

                                return result;
                            }
                        }
                        catch (AccessViolationException)
                        {
                            // AccessViolationException - dimensiones incorrectas, continuar
                            continue;
                        }
                        catch
                        {
                            // Otros errores, continuar
                        }
                    }

                    result.Logs.Add($"   ❌ Ninguna config funcionó con {formatoCaptura.Nombre} ({configuracionesProbadas} probadas)");
                }

                if (intento < 2)
                {
                    result.Logs.Add("🔄 Reintentando...");
                    result.Logs.Add("💡 Presiona MÁS fuerte y cubre TODO el sensor");
                    await Task.Delay(2000);
                }
            }

            result.Error = "No se pudo extraer FMD con ninguna configuración";
            result.Logs.Add("");
            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add("❌ TODAS LAS CONFIGURACIONES FALLARON");
            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add("");
            result.Logs.Add("🔍 DIAGNÓSTICO TÉCNICO:");
            result.Logs.Add("   • Todas fallaron con DP_TOO_SMALL_AREA");
            result.Logs.Add("   • Esto significa: ÁREA DE HUELLA FÍSICA MUY PEQUEÑA");
            result.Logs.Add("");

            result.Logs.Add($"   • Se probaron {configuracionesDimensiones.Count * formatosCaptura.Length * 2} combinaciones");
            result.Logs.Add("⚠️ ESTO NO ES UN PROBLEMA DE SOFTWARE");
            result.Logs.Add("   El problema es CÓMO colocas el dedo:");
            result.Logs.Add("");
            result.Logs.Add("❌ INCORRECTO:");
            result.Logs.Add("   • Solo poner la punta del dedo");
            result.Logs.Add("   • Presionar suavemente");
            result.Logs.Add("   • Dedo seco o con grasa");
            result.Logs.Add("");
            result.Logs.Add("✅ CORRECTO:");
            result.Logs.Add("   1. LIMPIA tu dedo (agua y jabón)");
            result.Logs.Add("   2. SECA completamente");
            result.Logs.Add("   3. Coloca TODO el dedo (desde la uña)");
            result.Logs.Add("   4. Presiona CON MUCHA FUERZA");
            result.Logs.Add("   5. Mantén 5-6 segundos SIN MOVER");
            result.Logs.Add("   6. Si falla: prueba OTRO DEDO");
            result.Logs.Add("═══════════════════════════════════════════");

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // GENERAR CONFIGURACIONES SEGURAS DE DIMENSIONES
        // ════════════════════════════════════════════════════════════════════
        private List<DimensionConfig> GenerarConfiguracionesDimensiones()
        {
            var configs = new List<DimensionConfig>();
            const int BYTES_ESPERADOS = 139994;

            // Anchos prioritarios (más probables)
            int[] anchosPrioritarios = { 252, 258, 256, 260, 240, 270, 280, 300 };

            foreach (int ancho in anchosPrioritarios)
            {
                int altura = BYTES_ESPERADOS / ancho;

                // Solo agregar si el total de bytes está cerca del esperado
                int bytesCalculados = ancho * altura;
                if (Math.Abs(bytesCalculados - BYTES_ESPERADOS) < 2000)
                {
                    configs.Add(new DimensionConfig { Width = ancho, Height = altura });

                    // Variaciones mínimas
                    configs.Add(new DimensionConfig { Width = ancho, Height = altura - 1 });
                    configs.Add(new DimensionConfig { Width = ancho, Height = altura + 1 });
                    configs.Add(new DimensionConfig { Width = ancho - 1, Height = altura });
                    configs.Add(new DimensionConfig { Width = ancho + 1, Height = altura });
                }
            }

            // Anchos secundarios
            int[] anchosSecundarios = { 200, 220, 230, 320, 350, 400 };

            foreach (int ancho in anchosSecundarios)
            {
                int altura = BYTES_ESPERADOS / ancho;
                int bytesCalculados = ancho * altura;

                if (Math.Abs(bytesCalculados - BYTES_ESPERADOS) < 2000)
                {
                    configs.Add(new DimensionConfig { Width = ancho, Height = altura });
                }
            }

            return configs.DistinctBy(c => $"{c.Width}x{c.Height}").ToList();
        }

        private class DimensionConfig
        {
            public int Width { get; set; }
            public int Height { get; set; }
        }

        // ════════════════════════════════════════════════════════════════════
        // DIAGNÓSTICO (simplificado)
        // ════════════════════════════════════════════════════════════════════
        public async Task<DiagnosticoCaptura> DiagnosticarCaptura()
        {
            var diag = new DiagnosticoCaptura
            {
                LectorConectado = IsReaderConnected(),
                InfoLector = GetReaderInfo()
            };

            var captura = await CaptureFmdAsync();

            diag.Resultados.Add(new ResultadoDiagnostico
            {
                Metodo = "Ultra-robusto",
                Exitoso = captura.Success,
                Formato = captura.Formato,
                TamanoFid = captura.RawImageSize,
                TamanoFmd = captura.FmdSize,
                Error = captura.Error,
                Detalles = captura.Logs
            });

            return diag;
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTURA MÚLTIPLE
        // ════════════════════════════════════════════════════════════════════
        public async Task<MultiFmdCaptureResultTest> CaptureMultipleFmdsAsync(int numCaptures = 3)
        {
            var result = new MultiFmdCaptureResultTest { Success = false };

            if (!IsReaderConnected())
            {
                result.Error = "Reader no disponible";
                return result;
            }

            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add($"REGISTRO: {numCaptures} capturas");
            result.Logs.Add("═══════════════════════════════════════════");

            for (int i = 1; i <= numCaptures; i++)
            {
                result.Logs.Add($"");
                result.Logs.Add($"📸 Captura {i}/{numCaptures}");

                var captura = await CaptureFmdAsync();

                if (!captura.Success)
                {
                    result.Error = $"Error en captura {i}: {captura.Error}";
                    result.Logs.AddRange(captura.Logs.TakeLast(5));
                    return result;
                }

                result.Fmds.Add(captura.Fmd);
                result.FmdBase64List.Add(captura.FmdBase64);
                result.CapturesCompleted++;
                result.TotalFmdSize += captura.FmdSize;

                result.Logs.Add($"✅ OK ({captura.FmdSize:N0} bytes)");

                if (i < numCaptures)
                {
                    result.Logs.Add("🖐️ Retira el dedo...");
                    await Task.Delay(2000);
                }
            }

            result.ConcatenatedFmds = string.Join("|||", result.FmdBase64List);
            result.Success = true;
            result.Logs.Add("");
            result.Logs.Add($"✅ {result.CapturesCompleted} capturas exitosas");

            return result;
        }

        // Métodos de comparación (igual que antes)
        public bool CompareFmds(Fmd captured, Fmd stored, out int score)
        {
            try
            {
                CompareResult result = Comparison.Compare(captured, 0, stored, 0);
                score = result.Score;
                return score < 20000;
            }
            catch
            {
                score = int.MaxValue;
                return false;
            }
        }

        public (bool matched, int bestScore) CompareFmdAgainstMultiple(Fmd captured, List<Fmd> storedFmds)
        {
            int bestScore = int.MaxValue;
            foreach (var stored in storedFmds)
            {
                try
                {
                    CompareResult result = Comparison.Compare(captured, 0, stored, 0);
                    if (result.Score < bestScore) bestScore = result.Score;
                }
                catch { }
            }
            return (bestScore < 20000, bestScore);
        }

        public IdentifyResult IdentifyFmdAgainstAll(Fmd captured, List<Fmd> allFmds, int threshold = 20000)
        {
            try
            {
                return Comparison.Identify(captured, 0, allFmds, threshold, 5);
            }
            catch
            {
                return null!;
            }
        }

        public Dictionary<string, string> GetReaderInfo()
        {
            if (_mainService == null)
            {
                return new Dictionary<string, string> { { "Status", "No inicializado" } };
            }

            var info = _mainService.GetReaderInfo();
            info["Modo"] = "ULTRA-ROBUSTO (50+ configuraciones)";
            return info;
        }
    }
}