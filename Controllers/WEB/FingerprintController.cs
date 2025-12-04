using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VidaFit.Data;
using VidaFitBackend.Models;
using VidaFitBackend.Services;
using System;
using System.Threading.Tasks;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class FingerprintController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFingerprintService _fingerprintService;

        public FingerprintController(AppDbContext context, IFingerprintService fingerprintService)
        {
            _context = context;
            _fingerprintService = fingerprintService;
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET: api/Fingerprint/status
        // Verifica si el lector está conectado y devuelve información
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet("status")]
        public IActionResult GetReaderStatus()
        {
            try
            {
                var isConnected = _fingerprintService.IsReaderConnected();
                var info = _fingerprintService.GetReaderInfo();

                return Ok(new
                {
                    connected = isConnected,
                    readerInfo = info,
                    message = isConnected ? "Lector conectado y listo" : "Lector no disponible"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    connected = false,
                    error = ex.Message
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST: api/Fingerprint/capture
        // Captura UNA huella y devuelve el template FMD
        // USO: Para pruebas o verificaciones individuales
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost("capture")]
        public IActionResult CaptureFingerprint()
        {
            try
            {
                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   🔵 API: Captura individual de huella");
                Console.WriteLine("═══════════════════════════════════════════");

                // Paso 1: Capturar imagen de huella (FID)
                byte[] fingerprintData = _fingerprintService.CaptureFingerprint();

                // Paso 2: Convertir a template FMD para comparación
                string fmdTemplate = _fingerprintService.ConvertToTemplate(fingerprintData);

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   ✅ API: Captura completada");
                Console.WriteLine($"   📊 Template FMD: {fmdTemplate.Length} caracteres");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return Ok(new
                {
                    success = true,
                    message = "Huella capturada y convertida a template FMD",
                    template = fmdTemplate,
                    templateSize = fmdTemplate.Length,
                    note = "Este template FMD está listo para comparación biométrica"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ❌ API: Error - {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    message = "No se pudo capturar la huella. Intente de nuevo."
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST: api/Fingerprint/register
        // Registra un nuevo cliente con 3 CAPTURAS de huella
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost("register")]
        public async Task<IActionResult> RegisterClientWithFingerprint([FromBody] RegisterFingerprintRequest request)
        {
            try
            {
                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   🔵 API: REGISTRO CON MÚLTIPLES HUELLAS");
                Console.WriteLine($"   Cliente: {request.Nombre} {request.Apellido}");
                Console.WriteLine("═══════════════════════════════════════════");

                // Validar que no exista cliente con la misma cédula
                var clienteExistente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Cedula == request.Cedula);

                if (clienteExistente != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Ya existe un cliente con esa cédula"
                    });
                }

                // ═════════════════════════════════════════════════════════════
                // CRÍTICO: Capturar 3 huellas y generar 3 templates FMD
                // Formato guardado: "FMD1|||FMD2|||FMD3"
                // ═════════════════════════════════════════════════════════════
                Console.WriteLine("   📸 Capturando múltiples huellas...");
                Console.WriteLine("   El usuario pondrá el dedo 3 veces");
                Console.WriteLine("");

                string multiTemplate = _fingerprintService.CaptureAndCreateMultiTemplate();

                // Verificar que se hayan capturado templates
                if (string.IsNullOrEmpty(multiTemplate))
                {
                    throw new Exception("No se generaron templates válidos");
                }

                int templateCount = multiTemplate.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries).Length;
                Console.WriteLine($"   ✅ Se generaron {templateCount} template(s) FMD");
                Console.WriteLine("");

                // Crear cliente con templates FMD
                var cliente = new Cliente
                {
                    Id = Guid.NewGuid(),
                    Nombre = request.Nombre,
                    Apellido = request.Apellido,
                    Cedula = request.Cedula,
                    Telefono = request.Telefono,
                    Email = request.Email,
                    FechaNacimiento = request.FechaNacimiento,
                    Direccion = request.Direccion,
                    HuellaDigital = multiTemplate,   // Templates FMD separados por |||
                    HuellaTemplate = multiTemplate,  // Mismo valor para compatibilidad
                    FotoBase64 = request.FotoBase64,
                    Activo = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ✅ Cliente registrado: {cliente.Nombre} {cliente.Apellido}");
                Console.WriteLine($"   ID: {cliente.Id}");
                Console.WriteLine($"   Templates FMD: {templateCount}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return Ok(new
                {
                    success = true,
                    message = $"Cliente registrado con {templateCount} template(s) de huella",
                    clienteId = cliente.Id,
                    templatesCount = templateCount,
                    cliente = new
                    {
                        cliente.Id,
                        cliente.Nombre,
                        cliente.Apellido,
                        cliente.Cedula,
                        cliente.Email,
                        hasFingerprint = true
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ❌ Error en registro: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    message = "Error al registrar cliente con huella"
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST: api/Fingerprint/update/{clienteId}
        // Actualiza la huella de un cliente existente con 3 NUEVAS CAPTURAS
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost("update/{clienteId}")]
        public async Task<IActionResult> UpdateClientFingerprint(Guid clienteId)
        {
            try
            {
                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Cliente no encontrado"
                    });
                }

                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   🔵 ACTUALIZANDO HUELLA");
                Console.WriteLine($"   Cliente: {cliente.Nombre} {cliente.Apellido}");
                Console.WriteLine("═══════════════════════════════════════════");

                // ═════════════════════════════════════════════════════════════
                // CORREGIDO: Capturar 3 nuevas huellas, no solo 1
                // ═════════════════════════════════════════════════════════════
                Console.WriteLine("   📸 Capturando nuevas huellas (3 intentos)...");
                string multiTemplate = _fingerprintService.CaptureAndCreateMultiTemplate();

                if (string.IsNullOrEmpty(multiTemplate))
                {
                    throw new Exception("No se generaron templates válidos");
                }

                int templateCount = multiTemplate.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries).Length;

                // Actualizar templates FMD
                cliente.HuellaDigital = multiTemplate;
                cliente.HuellaTemplate = multiTemplate;
                cliente.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   ✅ Huellas actualizadas exitosamente");
                Console.WriteLine($"   Nuevos templates: {templateCount}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return Ok(new
                {
                    success = true,
                    message = $"Huella actualizada con {templateCount} template(s) nuevos",
                    clienteId = cliente.Id,
                    templatesCount = templateCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ❌ Error: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    message = "Error al actualizar huella"
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST: api/Fingerprint/verify/{clienteId}
        // Verifica la huella de un cliente específico
        // Captura 1 huella y la compara contra los 3 templates guardados
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost("verify/{clienteId}")]
        public async Task<IActionResult> VerifyClientFingerprint(Guid clienteId)
        {
            try
            {
                var cliente = await _context.Clientes.FindAsync(clienteId);
                if (cliente == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Cliente no encontrado"
                    });
                }

                if (string.IsNullOrEmpty(cliente.HuellaTemplate))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "El cliente no tiene huella registrada"
                    });
                }

                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   🔍 VERIFICANDO HUELLA");
                Console.WriteLine($"   Cliente: {cliente.Nombre} {cliente.Apellido}");
                Console.WriteLine("═══════════════════════════════════════════");

                // Capturar huella actual (1 sola captura)
                Console.WriteLine("   📸 Capturando huella para verificar...");
                byte[] capturedFingerprint = _fingerprintService.CaptureFingerprint();

                // ═════════════════════════════════════════════════════════════
                // CRÍTICO: VerifyFingerprint ahora usa el motor de comparación
                // Compara la huella capturada contra los 3 templates guardados
                // ═════════════════════════════════════════════════════════════
                bool isMatch = _fingerprintService.VerifyFingerprint(
                    capturedFingerprint,
                    cliente.HuellaTemplate
                );

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   {(isMatch ? "✅ VERIFICACIÓN EXITOSA" : "❌ VERIFICACIÓN FALLIDA")}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return Ok(new
                {
                    success = true,
                    matched = isMatch,
                    message = isMatch
                        ? "Huella verificada correctamente"
                        : "La huella no coincide con ninguno de los templates registrados",
                    cliente = new
                    {
                        cliente.Id,
                        cliente.Nombre,
                        cliente.Apellido,
                        cliente.Cedula
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ❌ Error: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    message = "Error al verificar huella"
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST: api/Fingerprint/checkin
        // CHECK-IN AUTOMÁTICO: Captura huella y busca el cliente
        // Proceso: Capturar → Comparar con TODOS los clientes → Check-in
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost("checkin")]
        public async Task<IActionResult> CheckInWithFingerprint()
        {
            try
            {
                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   🔵 CHECK-IN AUTOMÁTICO CON HUELLA");
                Console.WriteLine("═══════════════════════════════════════════");

                // Paso 1: Capturar huella del usuario
                Console.WriteLine("   📸 Capturando huella...");
                byte[] capturedFingerprint = _fingerprintService.CaptureFingerprint();
                Console.WriteLine("   ✅ Huella capturada");
                Console.WriteLine("");

                // Paso 2: Buscar cliente comparando contra TODOS los registrados
                Console.WriteLine("   🔍 Buscando cliente en la base de datos...");

                var clientes = await _context.Clientes
                    .Where(c => c.Activo && c.HuellaTemplate != null)
                    .ToListAsync();

                Console.WriteLine($"   📋 Clientes activos con huella: {clientes.Count}");
                Console.WriteLine("");

                Cliente? clienteEncontrado = null;
                int clienteIndex = 0;

                // Comparar contra cada cliente
                foreach (var cliente in clientes)
                {
                    clienteIndex++;
                    Console.WriteLine($"   [{clienteIndex}/{clientes.Count}] Comparando con: {cliente.Nombre} {cliente.Apellido}");

                    // ═════════════════════════════════════════════════════════════
                    // CRÍTICO: Usa el motor de comparación biométrica
                    // Compara contra los 3 templates de cada cliente
                    // ═════════════════════════════════════════════════════════════
                    bool isMatch = _fingerprintService.VerifyFingerprint(
                        capturedFingerprint,
                        cliente.HuellaTemplate!
                    );

                    if (isMatch)
                    {
                        clienteEncontrado = cliente;
                        Console.WriteLine($"   ✅ COINCIDENCIA ENCONTRADA!");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"       ❌ No coincide");
                    }
                }

                Console.WriteLine("");

                // Si no se encontró ningún cliente
                if (clienteEncontrado == null)
                {
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("   ❌ NO SE ENCONTRÓ CLIENTE");
                    Console.WriteLine($"   Se comparó contra {clientes.Count} cliente(s)");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("");

                    return NotFound(new
                    {
                        success = false,
                        message = "No se encontró ningún cliente con esa huella",
                        searchedClients = clientes.Count,
                        action = "register"
                    });
                }

                // Paso 3: Verificar membresía activa
                Console.WriteLine("   🔍 Verificando membresía...");

                var membresiaActiva = await _context.Membresias
                    .Where(m => m.ClienteId == clienteEncontrado.Id &&
                               m.Estado == "Activa" &&
                               m.FechaVencimiento >= DateTime.UtcNow.Date)
                    .FirstOrDefaultAsync();

                if (membresiaActiva == null)
                {
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine($"   ⚠️  MEMBRESÍA VENCIDA");
                    Console.WriteLine($"   Cliente: {clienteEncontrado.Nombre} {clienteEncontrado.Apellido}");
                    Console.WriteLine("═══════════════════════════════════════════");
                    Console.WriteLine("");

                    return BadRequest(new
                    {
                        success = false,
                        message = $"Membresía vencida. Por favor renovar.",
                        cliente = new
                        {
                            clienteEncontrado.Id,
                            clienteEncontrado.Nombre,
                            clienteEncontrado.Apellido,
                            clienteEncontrado.Cedula
                        },
                        action = "renew"
                    });
                }

                Console.WriteLine($"   ✅ Membresía activa hasta: {membresiaActiva.FechaVencimiento:dd/MM/yyyy}");
                Console.WriteLine("");

                // Paso 4: Registrar check-in exitoso
                var checkIn = new CheckIn
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteEncontrado.Id,
                    FechaHora = DateTime.UtcNow,
                    Metodo = "huella",
                    Exitoso = true,
                    Nota = "Check-in automático con verificación biométrica",
                    CreatedAt = DateTime.UtcNow
                };

                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("   ✅ CHECK-IN EXITOSO");
                Console.WriteLine($"   Cliente: {clienteEncontrado.Nombre} {clienteEncontrado.Apellido}");
                Console.WriteLine($"   Hora: {checkIn.FechaHora:HH:mm:ss}");
                Console.WriteLine($"   Membresía válida hasta: {membresiaActiva.FechaVencimiento:dd/MM/yyyy}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return Ok(new
                {
                    success = true,
                    message = $"¡Bienvenido/a {clienteEncontrado.Nombre}!",
                    checkIn = new
                    {
                        checkIn.Id,
                        checkIn.FechaHora,
                        metodo = checkIn.Metodo
                    },
                    cliente = new
                    {
                        clienteEncontrado.Id,
                        clienteEncontrado.Nombre,
                        clienteEncontrado.Apellido,
                        clienteEncontrado.Cedula,
                        clienteEncontrado.FotoBase64
                    },
                    membresia = new
                    {
                        membresiaActiva.Id,
                        membresiaActiva.FechaVencimiento,
                        diasRestantes = (membresiaActiva.FechaVencimiento - DateTime.UtcNow.Date).Days
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine($"   ❌ ERROR: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    message = "Error al realizar check-in"
                });
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // DTO para registro de cliente
    // ══════════════════════════════════════════════════════════════════════
    public class RegisterFingerprintRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string? Direccion { get; set; }
        public string? FotoBase64 { get; set; }
    }
}