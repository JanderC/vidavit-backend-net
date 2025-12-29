using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DPUruNet;

namespace VidaFitBackend.Services
{
    public interface IFingerprintImageService
    {
        void Initialize();
        Task<ImageCaptureResult> CaptureImageAsync();
        Task<MultiImageCaptureResult> CaptureMultipleImagesAsync(int numCaptures = 3);
        Dictionary<string, string> GetReaderInfo();
        bool IsReaderConnected();
        double CompareImages(string imageBase64_1, string imageBase64_2);
        (bool matched, double bestSimilarity) CompareAgainstMultiple(string capturedImageBase64, List<string> storedImagesBase64);
    }

    public class ImageCaptureResult
    {
        public bool Success { get; set; }
        public string ImageBase64 { get; set; } = string.Empty;
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int ImageSize { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new();
    }

    public class MultiImageCaptureResult
    {
        public bool Success { get; set; }
        public List<string> ImagesBase64 { get; set; } = new();
        public int CapturesCompleted { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new();
    }

    public class FingerprintImageService : IFingerprintImageService
    {
        private Reader? _reader;
        private bool _isInitialized = false;
        private const int CAPTURE_TIMEOUT = 10000;

        public void Initialize()
        {
            try
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   SERVICIO DE IMÁGENES DE HUELLAS");
                Console.WriteLine("   (Basado en Test Exitoso)");
                Console.WriteLine("═══════════════════════════════════════════");

                Console.WriteLine("🔍 Llamando a ReaderCollection.GetReaders()...");

                ReaderCollection readers = ReaderCollection.GetReaders();

                if (readers == null)
                {
                    Console.WriteLine("❌ GetReaders() retornó NULL");
                    _isInitialized = false;
                    return;
                }

                Console.WriteLine($"✓ Se encontraron {readers.Count} lector(es)");

                if (readers.Count == 0)
                {
                    Console.WriteLine("❌ No se encontraron lectores conectados");
                    _isInitialized = false;
                    return;
                }

                // Usar el primer lector
                _reader = readers[0];

                Console.WriteLine($"📋 Lector detectado:");
                Console.WriteLine($"   Nombre: {_reader.Description.Name}");
                Console.WriteLine($"   Modalidad: {_reader.Description.Modality}");

                if (_reader.Description.SerialNumber != null)
                {
                    Console.WriteLine($"   Serial: {_reader.Description.SerialNumber}");
                }

                // Abrir el lector
                Console.WriteLine("🔌 Abriendo lector...");

                Constants.ResultCode result = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);

                if (result != Constants.ResultCode.DP_SUCCESS)
                {
                    Console.WriteLine($"❌ Error al abrir lector: {result}");
                    _reader = null;
                    _isInitialized = false;
                    return;
                }

                _isInitialized = true;
                Console.WriteLine("✅ Lector abierto correctamente");

                // Mostrar capacidades
                if (_reader.Capabilities != null && _reader.Capabilities.Resolutions != null)
                {
                    Console.WriteLine($"✓ Resoluciones disponibles: {_reader.Capabilities.Resolutions.Length}");
                    if (_reader.Capabilities.Resolutions.Length > 0)
                    {
                        Console.WriteLine($"✓ Resolución por defecto: {_reader.Capabilities.Resolutions[0]} DPI");
                    }
                }

                Console.WriteLine("═══════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPCIÓN: {ex.Message}");
                Console.WriteLine($"   Tipo: {ex.GetType().Name}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }

                _isInitialized = false;
                Console.WriteLine("═══════════════════════════════════════════");
            }
        }

        public bool IsReaderConnected()
        {
            if (!_isInitialized || _reader == null)
                return false;

            try
            {
                var status = _reader.GetStatus();
                return status == Constants.ResultCode.DP_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        public Dictionary<string, string> GetReaderInfo()
        {
            var info = new Dictionary<string, string>();

            if (!IsReaderConnected() || _reader == null)
            {
                info["Status"] = "Desconectado";
                info["Modo"] = "CAPTURA DE IMÁGENES";
                return info;
            }

            try
            {
                info["Status"] = "Conectado";
                info["Modo"] = "CAPTURA DE IMÁGENES";
                info["Modelo"] = _reader.Description.Name ?? "N/A";

                if (_reader.Description.Id != null)
                {
                    info["ID"] = _reader.Description.Id.ToString();
                }

                if (_reader.Description.SerialNumber != null)
                {
                    info["SerialNumber"] = _reader.Description.SerialNumber.ToString();
                }

                info["Modality"] = _reader.Description.Modality.ToString();

                if (_reader.Capabilities != null && _reader.Capabilities.Resolutions != null && _reader.Capabilities.Resolutions.Length > 0)
                {
                    info["Resolución"] = $"{_reader.Capabilities.Resolutions[0]} DPI";
                }
            }
            catch (Exception ex)
            {
                info["Error"] = ex.Message;
            }

            return info;
        }

        public async Task<ImageCaptureResult> CaptureImageAsync()
        {
            var result = new ImageCaptureResult { Success = false };

            if (!IsReaderConnected() || _reader == null)
            {
                result.Error = "Lector no disponible";
                result.Logs.Add("❌ Lector no está conectado");
                return result;
            }

            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add("📸 CAPTURANDO IMAGEN DE HUELLA");
            result.Logs.Add("═══════════════════════════════════════════");

            try
            {
                // Limpiar estado del lector
                try
                {
                    var status = _reader.GetStatus();
                    if (status == Constants.ResultCode.DP_SUCCESS &&
                        _reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                    {
                        _reader.CancelCapture();
                        await Task.Delay(500);
                        result.Logs.Add("⚠️ Lector ocupado - reiniciando...");
                    }
                }
                catch { }

                result.Logs.Add("👆 Coloca tu dedo en el lector...");

                int resolution = _reader.Capabilities.Resolutions[0];
                result.Logs.Add($"📏 Resolución: {resolution} DPI");

                CaptureResult captureResult = await Task.Run(() =>
                {
                    return _reader.Capture(
                        Constants.Formats.Fid.ANSI,
                        Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                        CAPTURE_TIMEOUT,
                        resolution
                    );
                });

                if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    result.Error = $"Error en captura: {captureResult.ResultCode}";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                if (captureResult.Data == null || captureResult.Data.Views == null || captureResult.Data.Views.Count == 0)
                {
                    result.Error = "No se capturó ninguna vista de la huella";
                    result.Logs.Add($"❌ {result.Error}");
                    return result;
                }

                result.Logs.Add("✅ Huella capturada correctamente");

                Fid.Fiv fiv = captureResult.Data.Views[0];

                result.Logs.Add($"📐 Dimensiones: {fiv.Width} x {fiv.Height}");
                result.Logs.Add($"💾 Tamaño imagen: {fiv.RawImage.Length:N0} bytes");

                using (Bitmap bitmap = CreateBitmapFromRawImage(fiv.RawImage, fiv.Width, fiv.Height))
                {
                    result.ImageBase64 = BitmapToBase64(bitmap);
                    result.ImageWidth = bitmap.Width;
                    result.ImageHeight = bitmap.Height;
                    result.ImageSize = fiv.RawImage.Length;
                }

                result.Success = true;
                result.Logs.Add("✅ Imagen convertida a Base64");
                result.Logs.Add("═══════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Excepción: {ex.Message}";
                result.Logs.Add($"❌ {result.Error}");
                return result;
            }
        }

        public async Task<MultiImageCaptureResult> CaptureMultipleImagesAsync(int numCaptures = 3)
        {
            var result = new MultiImageCaptureResult { Success = false };

            if (!IsReaderConnected())
            {
                result.Error = "Lector no disponible";
                return result;
            }

            result.Logs.Add("═══════════════════════════════════════════");
            result.Logs.Add($"📸 REGISTRO: {numCaptures} capturas");
            result.Logs.Add("═══════════════════════════════════════════");

            for (int i = 1; i <= numCaptures; i++)
            {
                result.Logs.Add("");
                result.Logs.Add($"📸 Captura {i}/{numCaptures}");

                var captura = await CaptureImageAsync();

                if (!captura.Success)
                {
                    result.Error = $"Error en captura {i}: {captura.Error}";
                    result.Logs.AddRange(captura.Logs.TakeLast(3));
                    return result;
                }

                result.ImagesBase64.Add(captura.ImageBase64);
                result.CapturesCompleted++;
                result.Logs.Add($"✅ Captura {i} OK");

                if (i < numCaptures)
                {
                    result.Logs.Add("🖐️ Retira tu dedo...");
                    await Task.Delay(2000);
                }
            }

            result.Success = true;
            result.Logs.Add("");
            result.Logs.Add($"✅ {result.CapturesCompleted} imágenes capturadas exitosamente");
            result.Logs.Add("═══════════════════════════════════════════");

            return result;
        }

        public double CompareImages(string imageBase64_1, string imageBase64_2)
        {
            try
            {
                using (Bitmap bmp1 = Base64ToBitmap(imageBase64_1))
                using (Bitmap bmp2 = Base64ToBitmap(imageBase64_2))
                {
                    if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                        return 0;

                    long totalPixels = (long)bmp1.Width * bmp1.Height;
                    long matchingPixels = 0;
                    int threshold = 30;

                    for (int y = 0; y < bmp1.Height; y++)
                    {
                        for (int x = 0; x < bmp1.Width; x++)
                        {
                            Color c1 = bmp1.GetPixel(x, y);
                            Color c2 = bmp2.GetPixel(x, y);
                            int diff = Math.Abs(c1.R - c2.R);

                            if (diff <= threshold)
                                matchingPixels++;
                        }
                    }

                    return (double)matchingPixels / totalPixels * 100.0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en comparación: {ex.Message}");
                return 0;
            }
        }

        public (bool matched, double bestSimilarity) CompareAgainstMultiple(string capturedImageBase64, List<string> storedImagesBase64)
        {
            double bestSimilarity = 0;

            foreach (var storedImage in storedImagesBase64)
            {
                double similarity = CompareImages(capturedImageBase64, storedImage);
                if (similarity > bestSimilarity)
                    bestSimilarity = similarity;
            }

            bool matched = bestSimilarity >= 70.0;
            return (matched, bestSimilarity);
        }

        private Bitmap CreateBitmapFromRawImage(byte[] rawImage, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

            ColorPalette palette = bitmap.Palette;
            for (int i = 0; i < 256; i++)
            {
                palette.Entries[i] = Color.FromArgb(i, i, i);
            }
            bitmap.Palette = palette;

            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            try
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    rawImage, 0, bitmapData.Scan0, rawImage.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        private string BitmapToBase64(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }

        private Bitmap Base64ToBitmap(string base64)
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                return new Bitmap(ms);
            }
        }
    }
}