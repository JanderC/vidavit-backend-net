using System;
using System.Collections.Generic;
using System.Linq;
using DPUruNet;
using System.Threading;

namespace VidaFitBackend.Services
{
    public interface IFingerprintService
    {
        void Initialize();
        bool IsReaderConnected();
        byte[] CaptureFingerprint();
        string ConvertToTemplate(byte[] fingerprintData);
        bool VerifyFingerprint(byte[] capturedData, string storedTemplate);
        string CaptureAndCreateMultiTemplate();
        void Dispose();
        Dictionary<string, string> GetReaderInfo();
        double GetSimilarityScore(byte[] capturedData, string storedTemplate);
        (bool matched, string clientName, double similarity) VerifyWithMultipleCaptures(string[] allStoredTemplates, string[] clientNames);
    }

    public class FingerprintService : IFingerprintService, IDisposable
    {
        private Reader _reader;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();

        public FingerprintService()
        {
        }

        public void Initialize()
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine("");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   DIAGNÓSTICO DE LECTOR DE HUELLAS");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("");

                    Console.WriteLine("[0/7] Verificando DLLs nativas...");
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] requiredDlls = { "DPUruNet.dll", "dpfpdd.dll", "dpfj.dll", "dpftrapi.dll" };

                    foreach (string dll in requiredDlls)
                    {
                        string path = System.IO.Path.Combine(baseDir, dll);
                        bool exists = System.IO.File.Exists(path);
                        Console.WriteLine($"   {(exists ? "✓" : "✗")} {dll} - {(exists ? "Encontrada" : "FALTA")}");
                    }
                    Console.WriteLine("");

                    Console.WriteLine("[1/7] Iniciando búsqueda de lectores...");
                    ReaderCollection readers = ReaderCollection.GetReaders();
                    Console.WriteLine($"[2/7] Resultado: {readers?.Count ?? 0} lector(es) encontrado(s)");

                    if (readers == null || readers.Count == 0)
                    {
                        Console.WriteLine("✗ ERROR: No se detectaron lectores");
                        _isInitialized = false;
                        return;
                    }

                    Console.WriteLine($"[3/7] ✓ Detectados {readers.Count} lector(es)");
                    _reader = readers[0];
                    Console.WriteLine($"[4/7] Seleccionado: {_reader.Description.Name}");

                    Constants.ResultCode result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
                    if (result != Constants.ResultCode.DP_SUCCESS)
                    {
                        result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                    }

                    if (result != Constants.ResultCode.DP_SUCCESS)
                    {
                        Console.WriteLine($"✗ ERROR: No se pudo abrir el lector");
                        _isInitialized = false;
                        return;
                    }

                    _isInitialized = true;
                    Console.WriteLine("[7/7] ✓ LECTOR ABIERTO CORRECTAMENTE");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("");
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

        public byte[] CaptureFingerprint()
        {
            if (!IsReaderConnected())
            {
                throw new InvalidOperationException("El lector de huellas no está conectado");
            }

            const int MAX_RETRIES = 5;
            int attemptNumber = 0;

            while (attemptNumber < MAX_RETRIES)
            {
                attemptNumber++;

                try
                {
                    Console.WriteLine("");
                    Console.WriteLine("══════════════════════════════════════════════════");
                    Console.WriteLine($"   📌 CAPTURANDO HUELLA (Intento {attemptNumber}/{MAX_RETRIES})");
                    Console.WriteLine("══════════════════════════════════════════════════");
                    Console.WriteLine("   👆 Coloque su dedo en el sensor...");
                    Console.WriteLine("");

                    Constants.ResultCode statusResult = _reader.GetStatus();
                    if (statusResult == Constants.ResultCode.DP_SUCCESS)
                    {
                        if (_reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                        {
                            Console.WriteLine("⚠️  Lector BUSY - Reseteando...");
                            _reader.CancelCapture();
                            Thread.Sleep(500);
                            _reader.Reset();
                            Thread.Sleep(1000);
                        }
                    }

                    int resolution = _reader.Capabilities.Resolutions[0];
                    Console.WriteLine($"🔧 Resolución: {resolution} DPI");
                    Console.WriteLine("");

                    Console.WriteLine("⏳ Probando ANSI DEFAULT...");
                    CaptureResult captureResult = _reader.Capture(
                        Constants.Formats.Fid.ANSI,
                        Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                        8000,
                        resolution
                    );

                    if (captureResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                        captureResult.Data != null &&
                        captureResult.Data.Views != null &&
                        captureResult.Data.Views.Count > 0 &&
                        captureResult.Data.Views[0].Bytes != null &&
                        captureResult.Data.Views[0].Bytes.Length > 0)
                    {
                        byte[] data = captureResult.Data.Views[0].Bytes;
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA (ANSI)");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }

                    Console.WriteLine("⏳ Probando ANSI PIV...");
                    captureResult = _reader.Capture(
                        Constants.Formats.Fid.ANSI,
                        Constants.CaptureProcessing.DP_IMG_PROC_PIV,
                        8000,
                        resolution
                    );

                    if (captureResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                        captureResult.Data != null &&
                        captureResult.Data.Views != null &&
                        captureResult.Data.Views.Count > 0 &&
                        captureResult.Data.Views[0].Bytes != null &&
                        captureResult.Data.Views[0].Bytes.Length > 0)
                    {
                        byte[] data = captureResult.Data.Views[0].Bytes;
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA (ANSI PIV)");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }

                    Console.WriteLine("⏳ Probando ISO...");
                    captureResult = _reader.Capture(
                        Constants.Formats.Fid.ISO,
                        Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                        8000,
                        resolution
                    );

                    if (captureResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                        captureResult.Data != null &&
                        captureResult.Data.Views != null &&
                        captureResult.Data.Views.Count > 0 &&
                        captureResult.Data.Views[0].Bytes != null &&
                        captureResult.Data.Views[0].Bytes.Length > 0)
                    {
                        byte[] data = captureResult.Data.Views[0].Bytes;
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA (ISO)");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }

                    Console.WriteLine($"⚠️  Intento {attemptNumber} falló");

                    if (attemptNumber < MAX_RETRIES)
                    {
                        Console.WriteLine($"🔄 Reintentando en 2 segundos...");
                        Thread.Sleep(2000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error en intento {attemptNumber}: {ex.Message}");

                    if (attemptNumber < MAX_RETRIES)
                    {
                        Thread.Sleep(2000);
                    }
                }
            }

            throw new Exception($"No se pudo capturar huella después de {MAX_RETRIES} intentos");
        }

        public string ConvertToTemplate(byte[] fingerprintData)
        {
            if (fingerprintData == null || fingerprintData.Length == 0)
            {
                throw new ArgumentException("Los datos de huella no pueden estar vacíos");
            }

            string base64 = Convert.ToBase64String(fingerprintData);
            return base64;
        }

        public bool VerifyFingerprint(byte[] capturedData, string storedTemplate)
        {
            if (capturedData == null || capturedData.Length == 0 || string.IsNullOrEmpty(storedTemplate))
            {
                return false;
            }

            try
            {
                string[] templates = storedTemplate.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                double bestSimilarity = 0.0;

                foreach (var template in templates)
                {
                    double similarity = GetSimilarityScore(capturedData, template);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                    }
                }

                return bestSimilarity >= 65.0;
            }
            catch
            {
                return false;
            }
        }

        public string CaptureAndCreateMultiTemplate()
        {
            if (!IsReaderConnected())
            {
                throw new InvalidOperationException("El lector de huellas no está conectado");
            }

            Console.WriteLine("");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("   📸 REGISTRO MULTI-CAPTURA (5 intentos)");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            List<string> templates = new List<string>();

            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    Console.WriteLine($"══════════ CAPTURA {i}/5 ══════════");

                    if (i > 1)
                    {
                        Console.WriteLine("👆 Levanta el dedo y vuelve a colocarlo");
                        Console.WriteLine("⏳ Esperando 3 segundos...");
                        Thread.Sleep(3000);
                    }

                    byte[] fingerprintData = CaptureFingerprint();
                    string template = ConvertToTemplate(fingerprintData);

                    templates.Add(template);

                    Console.WriteLine($"✅ Captura {i} exitosa");
                    Console.WriteLine("");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error en captura {i}: {ex.Message}");

                    if (i == 5 && templates.Count == 0)
                    {
                        throw new Exception("No se pudo capturar ninguna huella válida");
                    }

                    if (templates.Count >= 2)
                    {
                        Console.WriteLine($"💡 Continuando con {templates.Count} captura(s)");
                        break;
                    }
                }
            }

            if (templates.Count < 2)
            {
                throw new Exception($"Solo se capturaron {templates.Count} huella(s). Se requieren al menos 2.");
            }

            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine($"   ✅ REGISTRO COMPLETO: {templates.Count} huella(s)");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            return string.Join("|||", templates);
        }

        public double GetSimilarityScore(byte[] capturedData, string storedTemplate)
        {
            if (capturedData == null || storedTemplate == null)
                return 0.0;

            try
            {
                byte[] storedBytes = Convert.FromBase64String(storedTemplate);

                double sizeRatio = (double)Math.Min(capturedData.Length, storedBytes.Length) /
                                  Math.Max(capturedData.Length, storedBytes.Length);

                if (sizeRatio < 0.90) // Más permisivo
                    return 0.0;

                int minLength = Math.Min(capturedData.Length, storedBytes.Length);

                // ═════════════════════════════════════════════════════════════
                // MEJORA 1: Análisis por múltiples tamaños de sección
                // ═════════════════════════════════════════════════════════════
                List<double> sectionScores = new List<double>();

                // Secciones grandes (1000 bytes) - Patrón general
                int largeSectionSize = 1000;
                int largeSections = minLength / largeSectionSize;
                int matchingLargeSections = 0;

                for (int s = 0; s < largeSections; s++)
                {
                    int start = s * largeSectionSize;
                    int matches = 0;

                    for (int i = 0; i < largeSectionSize && (start + i) < minLength; i++)
                    {
                        int idx = start + i;
                        if (Math.Abs(capturedData[idx] - storedBytes[idx]) <= 25) // Tolerancia alta
                        {
                            matches++;
                        }
                    }

                    double secSim = (double)matches / largeSectionSize;
                    if (secSim >= 0.60)
                        matchingLargeSections++;
                }

                double largeScore = largeSections > 0
                    ? ((double)matchingLargeSections / largeSections) * 100.0
                    : 0.0;
                sectionScores.Add(largeScore);

                // Secciones medianas (500 bytes) - Balance
                int mediumSectionSize = 500;
                int mediumSections = minLength / mediumSectionSize;
                int matchingMediumSections = 0;

                for (int s = 0; s < mediumSections; s++)
                {
                    int start = s * mediumSectionSize;
                    int matches = 0;

                    for (int i = 0; i < mediumSectionSize && (start + i) < minLength; i++)
                    {
                        int idx = start + i;
                        if (Math.Abs(capturedData[idx] - storedBytes[idx]) <= 20)
                        {
                            matches++;
                        }
                    }

                    double secSim = (double)matches / mediumSectionSize;
                    if (secSim >= 0.65)
                        matchingMediumSections++;
                }

                double mediumScore = mediumSections > 0
                    ? ((double)matchingMediumSections / mediumSections) * 100.0
                    : 0.0;
                sectionScores.Add(mediumScore);

                // Secciones pequeñas (250 bytes) - Detalles
                int smallSectionSize = 250;
                int smallSections = minLength / smallSectionSize;
                int matchingSmallSections = 0;

                for (int s = 0; s < smallSections; s++)
                {
                    int start = s * smallSectionSize;
                    int matches = 0;

                    for (int i = 0; i < smallSectionSize && (start + i) < minLength; i++)
                    {
                        int idx = start + i;
                        if (Math.Abs(capturedData[idx] - storedBytes[idx]) <= 15)
                        {
                            matches++;
                        }
                    }

                    double secSim = (double)matches / smallSectionSize;
                    if (secSim >= 0.70)
                        matchingSmallSections++;
                }

                double smallScore = smallSections > 0
                    ? ((double)matchingSmallSections / smallSections) * 100.0
                    : 0.0;
                sectionScores.Add(smallScore);

                // ═════════════════════════════════════════════════════════════
                // MEJORA 2: Comparación byte-a-byte global con tolerancia
                // ═════════════════════════════════════════════════════════════
                int totalMatches = 0;
                for (int i = 0; i < minLength; i++)
                {
                    if (Math.Abs(capturedData[i] - storedBytes[i]) <= 20)
                    {
                        totalMatches++;
                    }
                }
                double globalScore = ((double)totalMatches / minLength) * 100.0;
                sectionScores.Add(globalScore);

                // ═════════════════════════════════════════════════════════════
                // MEJORA 3: Score ponderado (dar más peso a patrones consistentes)
                // ═════════════════════════════════════════════════════════════
                double weightedScore = (largeScore * 0.25) +    // Patrón general: 25%
                                      (mediumScore * 0.25) +     // Balance: 25%
                                      (smallScore * 0.20) +      // Detalles: 20%
                                      (globalScore * 0.30);      // Global: 30%

                return weightedScore;
            }
            catch
            {
                return 0.0;
            }
        }

        public (bool matched, string clientName, double similarity) VerifyWithMultipleCaptures(string[] allStoredTemplates, string[] clientNames)
        {
            Console.WriteLine("");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("   🔍 VERIFICACIÓN MULTI-CAPTURA (3 intentos)");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            List<byte[]> userCaptures = new List<byte[]>();

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    Console.WriteLine($"══════════ CAPTURA {i}/3 ══════════");

                    if (i > 1)
                    {
                        Console.WriteLine("👆 Levanta el dedo y vuelve a colocarlo");
                        Console.WriteLine("⏳ Esperando 2 segundos...");
                        Thread.Sleep(2000);
                    }

                    byte[] capture = CaptureFingerprint();
                    userCaptures.Add(capture);

                    Console.WriteLine($"✅ Captura {i} exitosa");
                    Console.WriteLine("");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error en captura {i}: {ex.Message}");

                    if (userCaptures.Count >= 2)
                    {
                        Console.WriteLine($"💡 Continuando con {userCaptures.Count} captura(s)");
                        break;
                    }
                }
            }

            if (userCaptures.Count < 2)
            {
                Console.WriteLine("❌ No se capturaron suficientes huellas");
                return (false, "", 0.0);
            }

            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine($"   ✅ {userCaptures.Count} CAPTURAS COMPLETADAS");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            Console.WriteLine("🔍 Comparando contra base de datos...");
            Console.WriteLine("");

            double bestSimilarity = 0.0;
            string bestClientName = "";

            for (int c = 0; c < allStoredTemplates.Length; c++)
            {
                Console.WriteLine($"[Cliente {c + 1}/{allStoredTemplates.Length}] {clientNames[c]}");

                string[] clientTemplates = allStoredTemplates[c].Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

                List<double> similarities = new List<double>();

                foreach (var userCapture in userCaptures)
                {
                    foreach (var clientTemplate in clientTemplates)
                    {
                        double similarity = GetSimilarityScore(userCapture, clientTemplate);
                        if (similarity > 0)
                        {
                            similarities.Add(similarity);
                        }
                    }
                }

                if (similarities.Count > 0)
                {
                    double avgSimilarity = similarities.OrderByDescending(s => s).Take(3).Average();

                    Console.WriteLine($"    📊 Similitud promedio: {avgSimilarity:F2}%");
                    Console.WriteLine($"    📊 Top 3: {string.Join(", ", similarities.OrderByDescending(s => s).Take(3).Select(s => $"{s:F1}%"))}");

                    if (avgSimilarity > bestSimilarity)
                    {
                        bestSimilarity = avgSimilarity;
                        bestClientName = clientNames[c];
                    }
                }
                else
                {
                    Console.WriteLine("    ❌ Sin coincidencias");
                }

                Console.WriteLine("");
            }

            double threshold = 35.0; // UMBRAL BAJADO de 60% a 35%
            bool isMatch = bestSimilarity >= threshold;

            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine($"   🏆 MEJOR COINCIDENCIA:");
            Console.WriteLine($"   Cliente: {bestClientName}");
            Console.WriteLine($"   Similitud: {bestSimilarity:F2}%");
            Console.WriteLine($"   Umbral: {threshold:F2}%");
            Console.WriteLine($"   Resultado: {(isMatch ? "✅ VERIFICADO" : "❌ NO COINCIDE")}");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            return (isMatch, bestClientName, bestSimilarity);
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
                        Console.WriteLine("✓ Lector cerrado correctamente");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cerrando lector: {ex.Message}");
                    }
                    finally
                    {
                        _reader = null;
                        _isInitialized = false;
                    }
                }
            }
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
                info["Modalidad"] = _reader.Description.Modality.ToString();
                info["Tecnología"] = _reader.Description.Technology.ToString();

                if (_reader.Capabilities != null)
                {
                    info["Resoluciones"] = string.Join(", ", _reader.Capabilities.Resolutions);
                    info["Puede Capturar"] = _reader.Capabilities.CanCapture.ToString();
                    info["Puede Transmitir"] = _reader.Capabilities.CanStream.ToString();
                }
            }
            catch (Exception ex)
            {
                info["Error"] = ex.Message;
            }

            return info;
        }
    }
}