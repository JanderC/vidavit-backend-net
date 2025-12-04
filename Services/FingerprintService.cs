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
    }

    public class FingerprintService : IFingerprintService, IDisposable
    {
        private Reader _reader;
        private bool _isInitialized = false;
        private const int PROBABILITY_ONE = 0x7fffffff;
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
                    Console.WriteLine("");
                    Console.WriteLine("Información del lector:");
                    Console.WriteLine("─────────────────────────────────────────");

                    for (int i = 0; i < readers.Count; i++)
                    {
                        Console.WriteLine($"Lector #{i + 1}:");
                        Console.WriteLine($"  Nombre:      {readers[i].Description.Name ?? "N/A"}");
                        Console.WriteLine($"  Serie:       {readers[i].Description.SerialNumber ?? "N/A"}");
                        Console.WriteLine($"  Modalidad:   {readers[i].Description.Modality}");
                        Console.WriteLine($"  Tecnología:  {readers[i].Description.Technology}");
                    }
                    Console.WriteLine("─────────────────────────────────────────");
                    Console.WriteLine("");

                    _reader = readers[0];
                    Console.WriteLine($"[4/7] Seleccionado: {_reader.Description.Name}");
                    Console.WriteLine("[5/7] Intentando abrir el lector...");

                    Console.WriteLine("   Probando modo EXCLUSIVE...");
                    Constants.ResultCode result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);

                    if (result != Constants.ResultCode.DP_SUCCESS)
                    {
                        Console.WriteLine($"   ⚠️  EXCLUSIVE falló ({result}), probando COOPERATIVE...");
                        result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                    }

                    Console.WriteLine($"[6/7] Código de resultado: {result}");

                    if (result != Constants.ResultCode.DP_SUCCESS)
                    {
                        Console.WriteLine($"✗ ERROR: No se pudo abrir el lector - Código: {result}");
                        _isInitialized = false;
                        return;
                    }

                    _isInitialized = true;
                    Console.WriteLine("[7/7] ✓ LECTOR ABIERTO CORRECTAMENTE");
                    Console.WriteLine("");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   ✓ LECTOR DE HUELLAS OPERATIVO");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("");
                }
                catch (System.IO.FileNotFoundException ex)
                {
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   ✗ ERROR: DLL NO ENCONTRADA");
                    Console.WriteLine($"   Archivo: {ex.FileName}");
                    Console.WriteLine("═══════════════════════════════════════════");
                    _isInitialized = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   ✗ ERROR INESPERADO");
                    Console.WriteLine($"   Tipo: {ex.GetType().Name}");
                    Console.WriteLine($"   Mensaje: {ex.Message}");
                    Console.WriteLine("═══════════════════════════════════════════");
                    _isInitialized = false;
                }
            }
        }

        public bool IsReaderConnected()
        {
            return _isInitialized && _reader != null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // SIMPLIFICADO: Solo captura bytes crudos del sensor
        // ══════════════════════════════════════════════════════════════════════
        public byte[] CaptureFingerprint()
        {
            if (!IsReaderConnected())
            {
                throw new InvalidOperationException("El lector de huellas no está conectado");
            }

            try
            {
                Console.WriteLine("");
                Console.WriteLine("══════════════════════════════════════════════════");
                Console.WriteLine("   📌 CAPTURANDO HUELLA DACTILAR");
                Console.WriteLine("══════════════════════════════════════════════════");
                Console.WriteLine("   👆 Coloque su dedo en el sensor...");
                Console.WriteLine("");

                // Verificar estado del lector
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

                // Intento 1: Captura estándar con ANSI
                Console.WriteLine("⏳ [Intento 1] Captura ANSI DEFAULT...");
                CaptureResult captureResult = _reader.Capture(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    10000,
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

                    // VERIFICAR que no sean todos ceros
                    bool allZeros = true;
                    for (int i = 0; i < Math.Min(1000, data.Length); i++)
                    {
                        if (data[i] != 0)
                        {
                            allZeros = false;
                            break;
                        }
                    }

                    if (!allZeros)
                    {
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.WriteLine($"   🔍 Calidad: {captureResult.Quality}");
                        Console.Write("   📊 Primeros 10 bytes: ");
                        for (int i = 0; i < Math.Min(10, data.Length); i++)
                        {
                            Console.Write($"{data[i]:X2} ");
                        }
                        Console.WriteLine("");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }
                    else
                    {
                        Console.WriteLine("⚠️  Captura ANSI DEFAULT dio todos ceros");
                    }
                }

                // Intento 2: Captura con PIV
                Console.WriteLine("");
                Console.WriteLine("⏳ [Intento 2] Captura ANSI PIV...");
                captureResult = _reader.Capture(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_PIV,
                    10000,
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

                    bool allZeros = true;
                    for (int i = 0; i < Math.Min(1000, data.Length); i++)
                    {
                        if (data[i] != 0)
                        {
                            allZeros = false;
                            break;
                        }
                    }

                    if (!allZeros)
                    {
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA (PIV)");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.Write("   📊 Primeros 10 bytes: ");
                        for (int i = 0; i < Math.Min(10, data.Length); i++)
                        {
                            Console.Write($"{data[i]:X2} ");
                        }
                        Console.WriteLine("");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }
                    else
                    {
                        Console.WriteLine("⚠️  Captura PIV dio todos ceros");
                    }
                }

                // Intento 3: Formato ISO
                Console.WriteLine("");
                Console.WriteLine("⏳ [Intento 3] Captura ISO...");
                captureResult = _reader.Capture(
                    Constants.Formats.Fid.ISO,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    10000,
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

                    bool allZeros = true;
                    for (int i = 0; i < Math.Min(1000, data.Length); i++)
                    {
                        if (data[i] != 0)
                        {
                            allZeros = false;
                            break;
                        }
                    }

                    if (!allZeros)
                    {
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("   ✅ CAPTURA EXITOSA (ISO)");
                        Console.WriteLine($"   📊 Tamaño: {data.Length:N0} bytes");
                        Console.Write("   📊 Primeros 10 bytes: ");
                        for (int i = 0; i < Math.Min(10, data.Length); i++)
                        {
                            Console.Write($"{data[i]:X2} ");
                        }
                        Console.WriteLine("");
                        Console.WriteLine("══════════════════════════════════════════════════");
                        Console.WriteLine("");
                        return data;
                    }
                    else
                    {
                        Console.WriteLine("⚠️  Captura ISO dio todos ceros");
                    }
                }

                // Intento 4: Streaming (último recurso)
                if (_reader.Capabilities.CanStream)
                {
                    Console.WriteLine("");
                    Console.WriteLine("⚠️ [Intento 4] Probando streaming...");
                    Console.WriteLine("   👆 Levante y vuelva a colocar el dedo...");
                    Thread.Sleep(1500);

                    Constants.ResultCode streamStart = _reader.StartStreaming();
                    if (streamStart == Constants.ResultCode.DP_SUCCESS)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            Thread.Sleep(500);
                            Console.Write(".");

                            CaptureResult streamResult = _reader.GetStreamImage(
                                Constants.Formats.Fid.ANSI,
                                Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                                resolution
                            );

                            if (streamResult.ResultCode == Constants.ResultCode.DP_SUCCESS &&
                                streamResult.Data != null &&
                                streamResult.Data.Bytes != null &&
                                streamResult.Data.Bytes.Length > 0)
                            {
                                _reader.StopStreaming();
                                byte[] fidBytes = streamResult.Data.Bytes;

                                // VERIFICAR que no sean todos ceros
                                bool allZeros = true;

                                for (int l = 0; l < Math.Min(1000, fidBytes.Length); l++)
                                {
                                    if (fidBytes[i] != 0)
                                    {
                                        allZeros = false;
                                        break;
                                    }
                                }

                                if (allZeros)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("⚠️  Datos streaming son todos ceros, continuando...");
                                    continue; // Seguir intentando
                                }

                                Console.WriteLine("");
                                Console.WriteLine("══════════════════════════════════════════════════");
                                Console.WriteLine("   ✅ CAPTURA EXITOSA (STREAMING)");
                                Console.WriteLine($"   📊 Tamaño: {fidBytes.Length:N0} bytes");
                                Console.Write("   📊 Primeros 10 bytes: ");
                                for (int j = 0; j < Math.Min(10, fidBytes.Length); j++)
                                {
                                    Console.Write($"{fidBytes[j]:X2} ");
                                }
                                Console.WriteLine("");
                                Console.WriteLine("══════════════════════════════════════════════════");
                                Console.WriteLine("");
                                return fidBytes;
                            }
                        }
                        Console.WriteLine("");
                        _reader.StopStreaming();
                    }
                }

                throw new Exception("No se pudo capturar huella");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CORREGIDO: Convierte bytes a Base64 correctamente
        // ══════════════════════════════════════════════════════════════════════
        public string ConvertToTemplate(byte[] fingerprintData)
        {
            if (fingerprintData == null || fingerprintData.Length == 0)
            {
                throw new ArgumentException("Los datos de huella no pueden estar vacíos");
            }

            try
            {
                Console.WriteLine("");
                Console.WriteLine("🔄 Convirtiendo huella a formato almacenable...");
                Console.WriteLine($"   📦 Tamaño entrada: {fingerprintData.Length:N0} bytes");

                // Verificar que los datos no sean todos iguales (corruptos)
                bool allSame = true;
                byte firstByte = fingerprintData[0];
                for (int i = 1; i < Math.Min(100, fingerprintData.Length); i++)
                {
                    if (fingerprintData[i] != firstByte)
                    {
                        allSame = false;
                        break;
                    }
                }

                if (allSame)
                {
                    Console.WriteLine("   ⚠️  ADVERTENCIA: Los primeros bytes son idénticos");
                    Console.WriteLine($"   ⚠️  Esto puede indicar datos corruptos");
                }

                // Mostrar primeros bytes para diagnóstico
                Console.Write("   📊 Primeros 20 bytes: ");
                for (int i = 0; i < Math.Min(20, fingerprintData.Length); i++)
                {
                    Console.Write($"{fingerprintData[i]:X2} ");
                }
                Console.WriteLine("");

                // Convertir a Base64
                string base64 = Convert.ToBase64String(fingerprintData);

                Console.WriteLine($"   ✅ Conversión exitosa");
                Console.WriteLine($"   📊 Base64 longitud: {base64.Length:N0} caracteres");
                Console.Write($"   📊 Primeros 50 chars: {base64.Substring(0, Math.Min(50, base64.Length))}");
                if (base64.Length > 50) Console.Write("...");
                Console.WriteLine("");

                // Verificar que el Base64 no sea todo "A"
                int aCount = base64.Count(c => c == 'A');
                double aPercentage = (double)aCount / base64.Length * 100.0;

                Console.WriteLine($"   📊 Caracteres 'A': {aPercentage:F1}%");

                if (aPercentage > 90)
                {
                    Console.WriteLine("   ⚠️  ADVERTENCIA: Más del 90% son 'A' - Datos posiblemente corruptos");
                }

                Console.WriteLine("");
                return base64;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error generando template: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // NUEVO: Comparación avanzada por similitud de patrones
        // ══════════════════════════════════════════════════════════════════════
        public bool VerifyFingerprint(byte[] capturedData, string storedTemplate)
        {
            if (capturedData == null || capturedData.Length == 0)
            {
                Console.WriteLine("✗ Datos de huella capturada vacíos");
                return false;
            }

            if (string.IsNullOrEmpty(storedTemplate))
            {
                Console.WriteLine("✗ Template almacenado vacío");
                return false;
            }

            try
            {
                Console.WriteLine("");
                Console.WriteLine("══════════════════════════════════════");
                Console.WriteLine("   🔍 VERIFICANDO HUELLA");
                Console.WriteLine("══════════════════════════════════════");

                // Separar templates múltiples
                string[] templates = storedTemplate.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"   📚 Templates almacenados: {templates.Length}");
                Console.WriteLine($"   📦 Huella capturada: {capturedData.Length:N0} bytes");
                Console.WriteLine("");

                double bestSimilarity = 0.0;
                int bestTemplateIndex = -1;

                // Comparar contra cada template
                for (int t = 0; t < templates.Length; t++)
                {
                    Console.WriteLine($"   [Template {t + 1}/{templates.Length}]");

                    try
                    {
                        byte[] storedBytes = Convert.FromBase64String(templates[t]);
                        Console.WriteLine($"       📦 Tamaño: {storedBytes.Length:N0} bytes");

                        // ═════════════════════════════════════════════════════════
                        // COMPARACIÓN MEJORADA: Múltiples algoritmos
                        // ═════════════════════════════════════════════════════════

                        // 1. Comparación de tamaño (debe ser similar)
                        double sizeRatio = (double)Math.Min(capturedData.Length, storedBytes.Length) /
                                          Math.Max(capturedData.Length, storedBytes.Length);

                        if (sizeRatio < 0.85)
                        {
                            Console.WriteLine($"       ⚠️  Tamaño muy diferente ({sizeRatio:P1})");
                            continue;
                        }

                        // 2. Comparación byte por byte con tolerancia
                        int minLength = Math.Min(capturedData.Length, storedBytes.Length);
                        int exactMatches = 0;
                        int tolerantMatches = 0;
                        int tolerance = 15; // Tolerancia de diferencia

                        for (int i = 0; i < minLength; i++)
                        {
                            if (capturedData[i] == storedBytes[i])
                            {
                                exactMatches++;
                                tolerantMatches++;
                            }
                            else if (Math.Abs(capturedData[i] - storedBytes[i]) <= tolerance)
                            {
                                tolerantMatches++;
                            }
                        }

                        double exactSimilarity = ((double)exactMatches / minLength) * 100.0;
                        double tolerantSimilarity = ((double)tolerantMatches / minLength) * 100.0;

                        // 3. Comparación por bloques (para detectar patrones)
                        int blockSize = 1000;
                        int totalBlocks = minLength / blockSize;
                        int matchingBlocks = 0;

                        for (int b = 0; b < totalBlocks; b++)
                        {
                            int blockStart = b * blockSize;
                            int blockMatches = 0;

                            for (int i = 0; i < blockSize && (blockStart + i) < minLength; i++)
                            {
                                if (Math.Abs(capturedData[blockStart + i] - storedBytes[blockStart + i]) <= tolerance)
                                {
                                    blockMatches++;
                                }
                            }

                            double currentBlockSimilarity = (double)blockMatches / blockSize;
                            if (currentBlockSimilarity > 0.75) // 75% del bloque similar
                            {
                                matchingBlocks++;
                            }
                        }

                        double blockSimilarity = totalBlocks > 0 ? ((double)matchingBlocks / totalBlocks) * 100.0 : 0;

                        // 4. Calcular similitud combinada
                        double combinedSimilarity = (tolerantSimilarity * 0.6) + (blockSimilarity * 0.4);

                        Console.WriteLine($"       📊 Exacta: {exactSimilarity:F2}%");
                        Console.WriteLine($"       📊 Tolerante: {tolerantSimilarity:F2}%");
                        Console.WriteLine($"       📊 Bloques: {blockSimilarity:F2}%");
                        Console.WriteLine($"       📈 Combinada: {combinedSimilarity:F2}%");

                        if (combinedSimilarity > bestSimilarity)
                        {
                            bestSimilarity = combinedSimilarity;
                            bestTemplateIndex = t;
                        }
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine($"       ⚠️  Base64 inválido");
                        continue;
                    }
                }

                // Evaluar resultado
                double threshold = 75.0; // Umbral del 75% para match
                bool isMatch = bestSimilarity >= threshold;

                Console.WriteLine("");
                Console.WriteLine("──────────────────────────────────────");
                Console.WriteLine($"   🏆 Mejor coincidencia: Template {bestTemplateIndex + 1}");
                Console.WriteLine($"   📈 Similitud: {bestSimilarity:F2}%");
                Console.WriteLine($"   🎯 Umbral: {threshold:F2}%");
                Console.WriteLine("──────────────────────────────────────");

                if (isMatch)
                {
                    Console.WriteLine("   ✅ HUELLA VERIFICADA");
                }
                else
                {
                    Console.WriteLine("   ❌ NO COINCIDE");
                    Console.WriteLine($"   💡 Diferencia: {(threshold - bestSimilarity):F2}%");
                }

                Console.WriteLine("══════════════════════════════════════");
                Console.WriteLine("");

                return isMatch;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Captura 3 huellas y las guarda como bytes (no FMD)
        // ══════════════════════════════════════════════════════════════════════
        public string CaptureAndCreateMultiTemplate()
        {
            if (!IsReaderConnected())
            {
                throw new InvalidOperationException("El lector de huellas no está conectado");
            }

            Console.WriteLine("");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("   📸 REGISTRO MULTI-CAPTURA (3 intentos)");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            List<string> templates = new List<string>();

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    Console.WriteLine($"══════════ CAPTURA {i}/3 ══════════");

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

                    if (i == 3 && templates.Count == 0)
                    {
                        throw new Exception("No se pudo capturar ninguna huella válida");
                    }

                    if (templates.Count > 0)
                    {
                        Console.WriteLine($"💡 Continuando con {templates.Count} captura(s) exitosa(s)");
                        break;
                    }
                }
            }

            if (templates.Count == 0)
            {
                throw new Exception("No se capturaron huellas válidas");
            }

            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine($"   ✅ REGISTRO COMPLETO: {templates.Count} huella(s)");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine("");

            return string.Join("|||", templates);
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