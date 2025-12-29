using System;
using System.Collections.Generic;
using System.Linq;
using DPUruNet;
using System.Threading;
using System.Threading.Tasks;

namespace VidaFitBackend.Services
{
    public interface IFingerprintService
    {
        void Initialize();
        bool IsReaderConnected();

        // Métodos FMD (nuevos - RECOMENDADOS)
        Task<FmdCaptureResult> CaptureFmdAsync();
        Task<MultiFmdCaptureResult> CaptureMultipleFmdsAsync(int numCaptures = 3);
        bool CompareFmds(Fmd captured, Fmd stored, out int score);
        (bool matched, int bestScore) CompareFmdAgainstMultiple(Fmd captured, List<Fmd> storedFmds);
        IdentifyResult IdentifyFmdAgainstAll(Fmd captured, List<Fmd> allFmds, int threshold = 20000);

        // Métodos legacy (compatibilidad)
        byte[] CaptureFingerprint();
        string ConvertToTemplate(byte[] fingerprintData);
        bool VerifyFingerprint(byte[] capturedData, string storedTemplate);
        string CaptureAndCreateMultiTemplate();
        void Dispose();
        Dictionary<string, string> GetReaderInfo();
        double GetSimilarityScore(byte[] capturedData, string storedTemplate);
        (bool matched, string clientName, double similarity) VerifyWithMultipleCaptures(string[] allStoredTemplates, string[] clientNames);
    }

    // ════════════════════════════════════════════════════════════════════
    // MODELOS DE RESPUESTA
    // ════════════════════════════════════════════════════════════════════
    public class FmdCaptureResult
    {
        public bool Success { get; set; }
        public Fmd Fmd { get; set; }
        public string FmdBase64 { get; set; }
        public int FmdSize { get; set; }
        public byte[] RawImage { get; set; }
        public int RawImageSize { get; set; }
        public string Error { get; set; }
        public List<string> Logs { get; set; } = new List<string>();
    }

    public class MultiFmdCaptureResult
    {
        public bool Success { get; set; }
        public List<Fmd> Fmds { get; set; } = new List<Fmd>();
        public List<string> FmdBase64List { get; set; } = new List<string>();
        public string ConcatenatedFmds { get; set; }  // FMD1|||FMD2|||FMD3
        public int CapturesCompleted { get; set; }
        public int TotalFmdSize { get; set; }
        public string Error { get; set; }
        public List<string> Logs { get; set; } = new List<string>();
    }

    public class FingerprintService : IFingerprintService, IDisposable
    {
        private Reader _reader;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();
        private const int DPFJ_PROBABILITY_ONE = 0x7fffffff;

        public void Initialize()
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   INICIALIZANDO DIGITALPERSONA 4500");
                    Console.WriteLine("═══════════════════════════════════════════");

                    ReaderCollection readers = ReaderCollection.GetReaders();

                    if (readers == null || readers.Count == 0)
                    {
                        Console.WriteLine("✗ No se detectaron lectores");
                        _isInitialized = false;
                        return;
                    }

                    _reader = readers[0];
                    Console.WriteLine($"✓ Lector: {_reader.Description.Name}");

                    Constants.ResultCode result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);

                    if (result != Constants.ResultCode.DP_SUCCESS)
                    {
                        Console.WriteLine($"✗ Error abriendo lector");
                        _isInitialized = false;
                        return;
                    }

                    _isInitialized = true;
                    Console.WriteLine("✓ LECTOR LISTO");
                    Console.WriteLine("═══════════════════════════════════════════");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ ERROR: {ex.Message}");
                    _isInitialized = false;
                }
            }
        }

        public bool IsReaderConnected()
        {
            return _isInitialized && _reader != null;
        }

        // ════════════════════════════════════════════════════════════════════
        // MÉTODO PRINCIPAL: CAPTURA Y EXTRACCIÓN DE FMD
        // ════════════════════════════════════════════════════════════════════
        public async Task<FmdCaptureResult> CaptureFmdAsync()
        {
            var result = new FmdCaptureResult { Success = false };

            if (!IsReaderConnected())
            {
                result.Error = "Lector no conectado";
                return result;
            }

            try
            {
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add("CAPTURA CON EXTRACCIÓN DE FMD");
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add("👆 Coloca tu dedo...");

                // PASO 1: Capturar Fid completo
                Fid fid = await Task.Run(() => CaptureFidObject());

                if (fid == null)
                {
                    result.Error = "Fid es null";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                result.Logs.Add($"✅ Fid capturado");
                result.Logs.Add($"   Fid.Bytes: {fid.Bytes?.Length ?? 0:N0} bytes");
                result.Logs.Add($"   Fid.Views count: {fid.Views?.Count ?? 0}");

                // Verificar que hay Views
                if (fid.Views == null || fid.Views.Count == 0)
                {
                    result.Error = "Fid.Views está vacío o null";
                    result.Logs.Add($"❌ {result.Error}");
                    result.Logs.Add($"   Intentando usar Fid.Bytes directamente...");

                    // Intentar con los bytes completos
                    result.RawImage = fid.Bytes;
                    result.RawImageSize = fid.Bytes.Length;

                    // Obtener resolución del lector
                    int resolution = _reader.Capabilities.Resolutions[0];

                    // Dimensiones típicas DigitalPersona 4500
                    int width = 258;
                    int height = 336;

                    result.Logs.Add($"   Usando dimensiones por defecto: {width}x{height}");
                    result.Logs.Add($"   Resolución: {resolution} DPI");
                    result.Logs.Add("🔍 Extrayendo FMD...");

                    DataResult<Fmd> fmdResult = FeatureExtraction.CreateFmdFromRaw(
                        fid.Bytes,
                        0,
                        0,
                        width,
                        height,
                        resolution,
                        Constants.Formats.Fmd.ANSI
                    );

                    if (fmdResult.ResultCode != Constants.ResultCode.DP_SUCCESS || fmdResult.Data == null)
                    {
                        result.Error = $"Error extrayendo FMD: {fmdResult.ResultCode}";
                        result.Logs.Add($"❌ {result.Error}");
                        return result;
                    }

                    result.Fmd = fmdResult.Data;
                    result.FmdBase64 = Convert.ToBase64String(fmdResult.Data.Bytes);
                    result.FmdSize = fmdResult.Data.Bytes.Length;
                    result.Success = true;

                    result.Logs.Add($"✅ FMD extraído: {result.FmdSize:N0} bytes");
                    result.Logs.Add($"📊 Compresión: {((double)result.RawImageSize / result.FmdSize):F1}x");
                    result.Logs.Add("═══════════════════════════════════════════");

                    return result;
                }

                // Si hay Views, usar el primer View
                result.RawImage = fid.Bytes;
                result.RawImageSize = fid.Bytes.Length;

                var view = fid.Views[0];
                int width2 = view.Width;
                int height2 = view.Height;
                int resolution2 = fid.Resolution;
                byte[] rawImageData = view.RawImage;

                result.Logs.Add($"✅ View encontrado");
                result.Logs.Add($"   Dimensiones: {width2}x{height2}");
                result.Logs.Add($"   Resolución: {resolution2} DPI");
                result.Logs.Add($"   RawImage: {rawImageData?.Length ?? 0:N0} bytes");

                if (rawImageData == null || rawImageData.Length == 0)
                {
                    result.Error = "RawImage está vacío";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                result.Logs.Add("🔍 Extrayendo FMD desde RawImage...");

                DataResult<Fmd> fmdResult2 = FeatureExtraction.CreateFmdFromRaw(
                    rawImageData,
                    0,
                    0,
                    width2,
                    height2,
                    resolution2,
                    Constants.Formats.Fmd.ANSI
                );

                if (fmdResult2.ResultCode != Constants.ResultCode.DP_SUCCESS || fmdResult2.Data == null)
                {
                    result.Error = $"Error extrayendo FMD: {fmdResult2.ResultCode}";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                result.Fmd = fmdResult2.Data;
                result.FmdBase64 = Convert.ToBase64String(fmdResult2.Data.Bytes);
                result.FmdSize = fmdResult2.Data.Bytes.Length;
                result.Success = true;

                result.Logs.Add($"✅ FMD extraído: {result.FmdSize:N0} bytes");
                result.Logs.Add($"📊 Compresión: {((double)result.RawImageSize / result.FmdSize):F1}x");
                result.Logs.Add("═══════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Logs.Add($"❌ Exception: {ex.Message}");
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTURA MÚLTIPLE CON FMD (PARA REGISTRO)
        // ════════════════════════════════════════════════════════════════════
        public async Task<MultiFmdCaptureResult> CaptureMultipleFmdsAsync(int numCaptures = 3)
        {
            var result = new MultiFmdCaptureResult { Success = false };

            try
            {
                result.Logs.Add("═══════════════════════════════════════════");
                result.Logs.Add($"CAPTURA MÚLTIPLE: {numCaptures} capturas");
                result.Logs.Add("═══════════════════════════════════════════");

                for (int i = 1; i <= numCaptures; i++)
                {
                    try
                    {
                        result.Logs.Add("");
                        result.Logs.Add($"📸 Captura {i}/{numCaptures}...");

                        if (i > 1)
                        {
                            result.Logs.Add("   Levanta y vuelve a colocar el dedo");
                            await Task.Delay(2000);
                        }

                        var captureResult = await CaptureFmdAsync();

                        if (captureResult.Success)
                        {
                            result.Fmds.Add(captureResult.Fmd);
                            result.FmdBase64List.Add(captureResult.FmdBase64);
                            result.TotalFmdSize += captureResult.FmdSize;

                            result.Logs.Add($"   ✅ Captura {i} OK ({captureResult.FmdSize} bytes)");
                        }
                        else
                        {
                            result.Logs.Add($"   ⚠️  Error captura {i}: {captureResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Logs.Add($"   ⚠️  Error captura {i}: {ex.Message}");
                    }
                }

                result.CapturesCompleted = result.Fmds.Count;

                if (result.Fmds.Count == 0)
                {
                    result.Error = "No se capturó ninguna huella";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                // Concatenar FMDs con separador
                result.ConcatenatedFmds = string.Join("|||", result.FmdBase64List);

                result.Success = true;
                result.Logs.Add("");
                result.Logs.Add($"✅ REGISTRO COMPLETO:");
                result.Logs.Add($"   Capturas: {result.CapturesCompleted}/{numCaptures}");
                result.Logs.Add($"   Tamaño total FMD: {result.TotalFmdSize:N0} bytes");
                result.Logs.Add($"   Tamaño promedio: {result.TotalFmdSize / result.CapturesCompleted:N0} bytes");
                result.Logs.Add("═══════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Logs.Add($"❌ Exception: {ex.Message}");
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // COMPARACIÓN 1:1 CON FMD (VERIFICACIÓN)
        // ════════════════════════════════════════════════════════════════════
        public bool CompareFmds(Fmd captured, Fmd stored, out int score)
        {
            score = int.MaxValue;

            if (captured == null || stored == null)
                return false;

            try
            {
                CompareResult compareResult = Comparison.Compare(captured, 0, stored, 0);

                if (compareResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
                {
                    score = compareResult.Score;

                    // Score: 0 = match perfecto, alto = no match
                    // Threshold típico: 20000
                    bool isMatch = score < 20000;

                    Console.WriteLine($"Score: {score} - {(isMatch ? "✅ MATCH" : "❌ NO MATCH")}");

                    return isMatch;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comparando: {ex.Message}");
            }

            return false;
        }

        // ════════════════════════════════════════════════════════════════════
        // COMPARACIÓN CONTRA MÚLTIPLES FMDs
        // ════════════════════════════════════════════════════════════════════
        public (bool matched, int bestScore) CompareFmdAgainstMultiple(Fmd captured, List<Fmd> storedFmds)
        {
            int bestScore = int.MaxValue;

            foreach (var stored in storedFmds)
            {
                if (CompareFmds(captured, stored, out int currentScore))
                {
                    if (currentScore < bestScore)
                    {
                        bestScore = currentScore;
                    }
                }
            }

            bool matched = bestScore < 20000;
            return (matched, bestScore);
        }

        // ════════════════════════════════════════════════════════════════════
        // IDENTIFICACIÓN 1:N (BUSCAR ENTRE MÚLTIPLES PERSONAS) ✅✅✅
        // ════════════════════════════════════════════════════════════════════
        public IdentifyResult IdentifyFmdAgainstAll(Fmd captured, List<Fmd> allFmds, int threshold = 20000)
        {
            try
            {
                // Este método es PERFECTO para identificar entre 100+ personas
                IdentifyResult result = Comparison.Identify(
                    captured,
                    0,              // índice de vista
                    allFmds,        // TODOS los FMDs de la DB
                    threshold,      // umbral (típico: 20000)
                    5               // retornar top 5 candidatos
                );

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en identificación: {ex.Message}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTURA RAW (PRIVADO - USADO POR CaptureFmdAsync)
        // ════════════════════════════════════════════════════════════════════
        private byte[] CaptureRawImage()
        {
            try
            {
                // Verificar estado
                Constants.ResultCode statusResult = _reader.GetStatus();
                if (statusResult == Constants.ResultCode.DP_SUCCESS)
                {
                    if (_reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                    {
                        _reader.CancelCapture();
                        Thread.Sleep(200);
                    }
                }

                int resolution = _reader.Capabilities.Resolutions[0];

                // MÉTODO 4: Acceso directo a Data.Bytes (el que funcionó)
                CaptureResult captureResult = _reader.Capture(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    8000,       // timeout
                    resolution
                );

                if (captureResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                    captureResult.Data?.Bytes != null &&
                    captureResult.Data.Bytes.Length > 0)
                {
                    return captureResult.Data.Bytes;
                }

                throw new Exception($"Captura falló: {captureResult.ResultCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error capturando: {ex.Message}");
            }
        }

        private Fid CaptureFidObject()
        {
            try
            {
                // Verificar estado
                Constants.ResultCode statusResult = _reader.GetStatus();
                if (statusResult == Constants.ResultCode.DP_SUCCESS)
                {
                    if (_reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                    {
                        _reader.CancelCapture();
                        Thread.Sleep(200);
                    }
                }

                int resolution = _reader.Capabilities.Resolutions[0];

                // Capturar y obtener el Fid completo desde CaptureResult
                CaptureResult captureResult = _reader.Capture(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    8000,
                    resolution
                );

                if (captureResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                    captureResult.Data != null)
                {
                    return captureResult.Data;
                }

                throw new Exception($"Captura falló: {captureResult.ResultCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error capturando Fid: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // MÉTODOS LEGACY (COMPATIBILIDAD)
        // ════════════════════════════════════════════════════════════════════
        public byte[] CaptureFingerprint()
        {
            return CaptureRawImage();
        }

        public string ConvertToTemplate(byte[] fingerprintData)
        {
            return Convert.ToBase64String(fingerprintData);
        }

        public bool VerifyFingerprint(byte[] capturedData, string storedTemplate)
        {
            // Legacy - usar FMD es mejor
            return false;
        }

        public string CaptureAndCreateMultiTemplate()
        {
            // Legacy - usar CaptureMultipleFmdsAsync es mejor
            List<string> templates = new List<string>();

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    byte[] data = CaptureFingerprint();
                    templates.Add(Convert.ToBase64String(data));
                }
                catch
                {
                    if (templates.Count >= 2) break;
                }
            }

            return string.Join("|||", templates);
        }

        public double GetSimilarityScore(byte[] capturedData, string storedTemplate)
        {
            // Legacy - usar CompareFmds es mejor
            return 0.0;
        }

        public (bool matched, string clientName, double similarity) VerifyWithMultipleCaptures(
            string[] allStoredTemplates,
            string[] clientNames)
        {
            // Legacy - usar IdentifyFmdAgainstAll es mejor
            return (false, "", 0.0);
        }

        public Dictionary<string, string> GetReaderInfo()
        {
            var info = new Dictionary<string, string>();

            if (!IsReaderConnected())
            {
                info["Status"] = "Desconectado";
                return info;
            }

            try
            {
                info["Status"] = "Conectado";
                info["Modelo"] = _reader.Description.Name ?? "N/A";
                info["Serie"] = _reader.Description.SerialNumber ?? "N/A";

                if (_reader.Capabilities != null)
                {
                    info["Resoluciones"] = string.Join(", ", _reader.Capabilities.Resolutions);
                    info["CanCapture"] = _reader.Capabilities.CanCapture.ToString();
                    info["CanStream"] = _reader.Capabilities.CanStream.ToString();
                }
            }
            catch (Exception ex)
            {
                info["Error"] = ex.Message;
            }

            return info;
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_reader != null)
                {
                    try
                    {
                        _reader.Dispose();
                        Console.WriteLine("✓ Lector cerrado");
                    }
                    catch { }
                    finally
                    {
                        _reader = null;
                        _isInitialized = false;
                    }
                }
            }
        }
    }
}