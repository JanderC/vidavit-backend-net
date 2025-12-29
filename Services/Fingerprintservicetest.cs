using System;
using System.Collections.Generic;
using System.Linq;
using DPUruNet;
using System.Threading;
using System.Threading.Tasks;

namespace VidaFitBackend.Services
{
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
    /// Servicio de prueba INDEPENDIENTE con acceso directo al Reader
    /// USA EL MÉTODO CORRECTO: CreateFmdFromFid
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
                Console.WriteLine("   SERVICIO DE PRUEBA INDEPENDIENTE");
                Console.WriteLine("═══════════════════════════════════════════");

                _mainService = fingerprintService;

                // Obtener el Reader del servicio principal usando reflection
                var readerField = fingerprintService.GetType().GetField("_reader",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (readerField != null)
                {
                    _reader = readerField.GetValue(fingerprintService) as Reader;
                }

                if (_reader != null && fingerprintService.IsReaderConnected())
                {
                    _isInitialized = true;
                    Console.WriteLine("✓ Servicio de prueba con acceso directo al Reader");
                    Console.WriteLine("✓ USANDO MÉTODO CORRECTO: CreateFmdFromFid");
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
        // CAPTURA DIRECTA CON MÉTODO CORRECTO
        // ════════════════════════════════════════════════════════════════════
        public async Task<FmdCaptureResultTest> CaptureFmdAsync()
        {
            var result = new FmdCaptureResultTest
            {
                Success = false,
                Formato = "ANSI"
            };

            if (!IsReaderConnected() || _reader == null)
            {
                result.Error = "Reader no disponible";
                return result;
            }

            try
            {
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add("CAPTURA DIRECTA CON MÉTODO CORRECTO");
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add("👆 Coloca tu dedo COMPLETAMENTE en el sensor...");
                result.Logs.Add("💡 Presiona firmemente durante 3 segundos");

                // Capturar Fid directamente
                Fid fid = await Task.Run(() => CaptureFidDirecto());

                if (fid == null || fid.Bytes == null)
                {
                    result.Error = "Error capturando Fid";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                result.Logs.Add($"✅ Fid capturado: {fid.Bytes.Length:N0} bytes");
                result.Logs.Add($"   Views: {fid.Views?.Count ?? 0}");

                if (fid.Views != null && fid.Views.Count > 0)
                {
                    result.Logs.Add($"   ✓ View[0] - Width: {fid.Views[0].Width}, Height: {fid.Views[0].Height}");
                }
                else
                {
                    result.Logs.Add($"   ⚠️ Views está vacío - usando método alternativo");
                }

                result.RawImage = fid.Bytes;
                result.RawImageSize = fid.Bytes.Length;

                // ═══════════════════════════════════════════════════════════
                // EXTRACCIÓN DE FMD
                // ═══════════════════════════════════════════════════════════
                result.Logs.Add("🔍 Extrayendo FMD...");

                DataResult<Fmd> fmdResult;

                // Si Views está vacío, usar CreateFmdFromRaw
                if (fid.Views == null || fid.Views.Count == 0)
                {
                    result.Logs.Add("⚠️ Views vacío - Usando CreateFmdFromRaw...");

                    // Usar dimensiones estándar del DigitalPersona 4500
                    int width = 252;   // Ancho típico
                    int height = 554;  // Alto calculado: 139994 bytes / 252 ≈ 555

                    result.Logs.Add($"   Dimensiones: {width}x{height}");
                    result.Logs.Add($"   Bytes: {fid.Bytes.Length:N0}");
                    result.Logs.Add($"   Resolución: 500 DPI");

                    fmdResult = FeatureExtraction.CreateFmdFromRaw(
                        fid.Bytes,
                        0,                                    // fingerPosition
                        0,                                    // cbeffId
                        width,
                        height,
                        500,                                  // resolution (DPI)
                        Constants.Formats.Fmd.ANSI
                    );
                }
                else
                {
                    result.Logs.Add($"✓ Usando CreateFmdFromFid (Views disponible)");
                    fmdResult = FeatureExtraction.CreateFmdFromFid(
                        fid,
                        Constants.Formats.Fmd.ANSI
                    );
                }

                if (fmdResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    result.Error = $"Error: {fmdResult.ResultCode}";
                    result.Logs.Add($"❌ {result.Error}");

                    if (fmdResult.ResultCode.ToString().Contains("TOO_SMALL"))
                    {
                        result.Logs.Add($"");
                        result.Logs.Add($"💡 EL ÁREA DETECTADA ES MUY PEQUEÑA:");
                        result.Logs.Add($"   ✓ Datos capturados: {result.RawImageSize:N0} bytes");
                        result.Logs.Add($"   ✗ Pero la huella es muy pequeña");
                        result.Logs.Add($"");
                        result.Logs.Add($"   SOLUCIÓN:");
                        result.Logs.Add($"   1. Coloca TODO tu dedo en el sensor");
                        result.Logs.Add($"   2. Cubre la mayor área posible");
                        result.Logs.Add($"   3. Presiona MÁS firmemente (sin mover)");
                        result.Logs.Add($"   4. Mantén 3-4 segundos");
                        result.Logs.Add($"   5. Asegúrate que el dedo esté limpio y seco");
                    }
                    else if (fmdResult.ResultCode.ToString().Contains("INVALID"))
                    {
                        result.Logs.Add($"");
                        result.Logs.Add($"💡 FORMATO INVÁLIDO:");
                        result.Logs.Add($"   Posibles causas:");
                        result.Logs.Add($"   - Dedo no detectado correctamente");
                        result.Logs.Add($"   - Huella muy borrosa o húmeda");
                        result.Logs.Add($"   - Intenta de nuevo con el dedo limpio y seco");
                    }

                    return result;
                }

                if (fmdResult.Data == null || fmdResult.Data.Bytes == null)
                {
                    result.Error = "FMD es null";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                // Éxito
                result.Fmd = fmdResult.Data;
                result.FmdBase64 = Convert.ToBase64String(fmdResult.Data.Bytes);
                result.FmdSize = fmdResult.Data.Bytes.Length;
                result.Success = true;

                result.Logs.Add($"✅ ÉXITO");
                result.Logs.Add($"   FMD extraído: {result.FmdSize:N0} bytes");
                result.Logs.Add($"   Compresión: {((double)result.RawImageSize / result.FmdSize):F1}x");
                result.Logs.Add("═══════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Exception: {ex.Message}";
                result.Logs.Add($"❌ {result.Error}");
                result.Logs.Add($"   Stack: {ex.StackTrace}");
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTURA DIRECTA DEL FID
        // ════════════════════════════════════════════════════════════════════
        private Fid CaptureFidDirecto()
        {
            if (_reader == null)
            {
                throw new Exception("Reader es null");
            }

            try
            {
                // Limpiar estado
                Constants.ResultCode statusResult = _reader.GetStatus();
                if (statusResult == Constants.ResultCode.DP_SUCCESS)
                {
                    if (_reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                    {
                        _reader.CancelCapture();
                        Thread.Sleep(500);
                    }
                }

                // Obtener resolución
                int[] resolutions = _reader.Capabilities.Resolutions;
                int resolution = resolutions[0];

                // Capturar con procesamiento por defecto
                CaptureResult captureResult = _reader.Capture(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    15000,
                    resolution
                );

                if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    throw new Exception($"Captura falló: {captureResult.ResultCode}");
                }

                if (captureResult.Data == null)
                {
                    throw new Exception("captureResult.Data es null");
                }

                return captureResult.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DIAGNÓSTICO
        // ════════════════════════════════════════════════════════════════════
        public async Task<DiagnosticoCaptura> DiagnosticarCaptura()
        {
            var diagnostico = new DiagnosticoCaptura
            {
                LectorConectado = IsReaderConnected(),
                InfoLector = GetReaderInfo()
            };

            if (!IsReaderConnected())
            {
                return diagnostico;
            }

            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine("DIAGNÓSTICO CON MÉTODO CORRECTO");
            Console.WriteLine("═══════════════════════════════════════════");

            var result = new ResultadoDiagnostico { Metodo = "CreateFmdFromFid", Formato = "ANSI" };
            try
            {
                Console.WriteLine("📸 Coloca tu dedo...");
                var captura = await CaptureFmdAsync();
                result.Exitoso = captura.Success;
                result.TamanoFid = captura.RawImageSize;
                result.TamanoFmd = captura.FmdSize;
                result.Error = captura.Error;
                result.Detalles = captura.Logs;

                Console.WriteLine(result.Exitoso ? "✅ OK" : $"❌ {result.Error}");
            }
            catch (Exception ex)
            {
                result.Exitoso = false;
                result.Error = ex.Message;
            }

            diagnostico.Resultados.Add(result);
            return diagnostico;
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

            try
            {
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
                        result.Logs.AddRange(captura.Logs);
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
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add($"✅ {result.CapturesCompleted} capturas exitosas");
                result.Logs.Add("═══════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Logs.Add($"❌ {result.Error}");
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // COMPARACIÓN
        // ════════════════════════════════════════════════════════════════════
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
            info["Modo"] = "INDEPENDIENTE - Método correcto (CreateFmdFromFid)";
            return info;
        }
    }
}