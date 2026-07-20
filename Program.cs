using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VidaFit.Data;
using Microsoft.OpenApi.Models;
using VidaFitBackend.Services;
using VidaFit.Services;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// ✅ NECESARIO: Permite usar DateTime.Now sin conversiones a UTC
// Esto hace que PostgreSQL acepte timestamps sin zona horaria (timestamp without time zone)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ==================== CONFIGURACIÓN DE SERVICIOS ====================

// Servicios para MVC (Vistas Razor)
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation(); // Hot reload en desarrollo

// Servicios para API REST
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // PascalCase
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// CORS para permitir llamadas desde JavaScript
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// ✅ PostgreSQL - Usar configuración del appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "VidaFit2024SecretKey_MinimumLength32Chars!";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// Servicios personalizados
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IFingerprintService, FingerprintService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ✅ Registrar CajaFuerteController como servicio para inyección directa en CajaController
// (evita el HttpClient interno que causaba fallos al enviar cierres a Caja Fuerte)
builder.Services.AddScoped<VidaFit.Controllers.API.CajaFuerteController>();

// ⚠️ SERVICIOS AUTOMÁTICOS REMOVIDOS - Causan problemas con zonas horarias
// builder.Services.AddHostedService<ConsolidacionMensualService>();
// builder.Services.AddHostedService<CierreCajaAutomaticoService>();

// HttpClient para comunicación entre controladores
builder.Services.AddHttpClient();

// Sesiones para el panel admin
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Swagger para documentación de API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "TAURO GYM API",
        Version = "v1",
        Description = "Sistema de Gestión de Gimnasio con Check-in Biométrico"
    });
});

var app = builder.Build();

// ==================== INICIALIZACIÓN ====================

// Inicializar el servicio de huellas ANTES del banner
var fingerprintService = app.Services.GetRequiredService<IFingerprintService>();
fingerprintService.Initialize();

// Mostrar zona horaria del servidor
var zonaHoraria = TimeZoneInfo.Local;
var horaActual = DateTime.Now;

// Mostrar banner

Console.WriteLine("   Sistema de Gestión de Gimnasio POWER FITNESS GYM");
Console.WriteLine("   Versión 1.0 - Sistema Integrado");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine($"   ✓ Servidor iniciado en: http://localhost:5000");
Console.WriteLine($"   ✓ Base de datos: PostgreSQL - {connectionString?.Split(';')[0]}");
Console.WriteLine($"   ✓ Zona horaria: {zonaHoraria.DisplayName}");
Console.WriteLine($"   ✓ Hora actual: {horaActual:dd/MM/yyyy HH:mm:ss}");
Console.WriteLine($"   ✓ Timestamp mode: Legacy (DateTime.Now compatible)");
Console.WriteLine($"   {(fingerprintService.IsReaderConnected() ? "✓" : "✗")} Lector de huellas: {(fingerprintService.IsReaderConnected() ? "Conectado ✓" : "No detectado ✗")}");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   📱 Kiosko de Check-in: http://localhost:5000/kiosko");
Console.WriteLine("   🎛️  Panel Admin: http://localhost:5000/admin");
Console.WriteLine("   📊 API REST: http://localhost:5000/api");
Console.WriteLine("   📚 Swagger: http://localhost:5000/swagger");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   ⚠️  NOTA: Servicios automáticos deshabilitados");
Console.WriteLine("   ℹ️  Cierres de caja y consolidaciones deben hacerse manualmente");
Console.WriteLine("═══════════════════════════════════════════════════════");

// ==================== CONFIGURACIÓN DEL PIPELINE HTTP ====================

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TAURO GYM API v1"));
}

// Archivos estáticos (CSS, JS, imágenes)
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// ==================== RUTAS ====================

// Rutas para API REST (prefijo /api)
app.MapControllers();

// Rutas para vistas MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ruta específica para el kiosko
app.MapControllerRoute(
    name: "kiosko",
    pattern: "kiosko",
    defaults: new { controller = "Kiosko", action = "Index" });

app.MapControllerRoute(
    name: "cajafuerte",
    pattern: "cajafuerte/{action=Index}/{id?}",
    defaults: new { controller = "CajaFuerte" }
);

// ==================== ABRIR NAVEGADOR AUTOMÁTICAMENTE ====================

// Configurar el hook de inicio de la aplicación
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    // Esperar un momento para asegurar que el servidor esté completamente listo
    Task.Run(async () =>
    {
        await Task.Delay(1500); // Espera 1.5 segundos

        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("   ⚡ Abriendo navegador automáticamente...");
        Console.WriteLine("═══════════════════════════════════════════════════════");

        // Abrir Panel Admin
        OpenBrowser("http://localhost:5000/admin");
        Console.WriteLine("   ✓ Panel Admin abierto");

        // Pequeña pausa entre ventanas
        await Task.Delay(800);

        // Abrir Kiosko
        OpenBrowser("http://localhost:5000/kiosko");
        Console.WriteLine("   ✓ Kiosko abierto");

        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("   ✅ Sistema listo para usar");
        Console.WriteLine("   ⚠️  Presiona Ctrl+C para detener el servidor");
        Console.WriteLine("═══════════════════════════════════════════════════════");
    });
});

// ==================== INICIAR APLICACIÓN ====================

app.Run("http://localhost:5000");

// ==================== FUNCIÓN PARA ABRIR NAVEGADOR ====================

static void OpenBrowser(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows - Usar cmd para abrir en el navegador predeterminado
            var psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start {url}",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS
            Process.Start("open", url);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ⚠️  No se pudo abrir el navegador: {ex.Message}");
        Console.WriteLine($"   📌 Abre manualmente: {url}");
    }
}