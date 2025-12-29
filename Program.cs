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

var builder = WebApplication.CreateBuilder(args);

// ==================== CONFIGURACIÓN DE SERVICIOS ====================

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=vidafit;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// ════════════════════════════════════════════════════════════════════
// ⚠️ CRÍTICO: SOLO REGISTRAR EL SERVICIO DE IMÁGENES
// NO registrar otros servicios de huellas que bloqueen el lector
// ════════════════════════════════════════════════════════════════════

// ❌ COMENTADOS - Estos servicios bloquean el lector
// builder.Services.AddSingleton<IFingerprintService, FingerprintService>();
// builder.Services.AddSingleton<IFingerprintServiceTest, FingerprintServiceTest>();

// ✅ SOLO ESTE SERVICIO
builder.Services.AddSingleton<IFingerprintImageService, FingerprintImageService>();

// Otros servicios
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "VIDA FIT API",
        Version = "v1",
        Description = "Sistema de Gestión de Gimnasio con Check-in Biométrico"
    });
});

var app = builder.Build();

// ==================== INICIALIZACIÓN ====================

// ❌ NO INICIALIZAR OTROS SERVICIOS DE HUELLAS
// Solo el servicio de imágenes

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   🔍 INICIALIZANDO SERVICIO DE HUELLAS (IMÁGENES)");
Console.WriteLine("═══════════════════════════════════════════════════════");

var fingerprintImageService = app.Services.GetRequiredService<IFingerprintImageService>();
fingerprintImageService.Initialize();

Console.WriteLine("═══════════════════════════════════════════════════════");

// Banner principal
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   ██╗   ██╗██╗██████╗  █████╗     ███████╗██╗████████╗");
Console.WriteLine("   ██║   ██║██║██╔══██╗██╔══██╗    ██╔════╝██║╚══██╔══╝");
Console.WriteLine("   ██║   ██║██║██║  ██║███████║    █████╗  ██║   ██║   ");
Console.WriteLine("   ╚██╗ ██╔╝██║██║  ██║██╔══██║    ██╔══╝  ██║   ██║   ");
Console.WriteLine("    ╚████╔╝ ██║██████╔╝██║  ██║    ██║     ██║   ██║   ");
Console.WriteLine("     ╚═══╝  ╚═╝╚═════╝ ╚═╝  ╚═╝    ╚═╝     ╚═╝   ╚═╝   ");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   Sistema de Gestión de Gimnasio");
Console.WriteLine("   Versión 1.0 - API + MVC Híbrido");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine($"   ✓ Servidor: http://localhost:5000");
Console.WriteLine($"   ✓ Base de datos: PostgreSQL");

// Estado del lector
if (fingerprintImageService.IsReaderConnected())
{
    Console.WriteLine($"   ✅ Lector de huellas: CONECTADO");

    var info = fingerprintImageService.GetReaderInfo();
    if (info.ContainsKey("Resolución"))
    {
        Console.WriteLine($"   📏 {info["Resolución"]}");
    }
}
else
{
    Console.WriteLine($"   ❌ Lector de huellas: NO DETECTADO");
}

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   📱 Kiosko: http://localhost:5000/kiosko");
Console.WriteLine("   🎛️  Admin: http://localhost:5000/admin");
Console.WriteLine("   📊 API: http://localhost:5000/api");
Console.WriteLine("   📚 Swagger: http://localhost:5000/swagger");
Console.WriteLine("   🖼️  Huellas: /api/HuellaImagen");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("");
Console.WriteLine("   🚀 Abriendo navegador...");
Console.WriteLine("");
Console.WriteLine("   ⚠️  NO CIERRES ESTA VENTANA");
Console.WriteLine("   Presiona Ctrl+C para detener");
Console.WriteLine("═══════════════════════════════════════════════════════");

Task.Run(async () =>
{
    await Task.Delay(1500);
    OpenBrowser("http://localhost:5000/admin");
});

// ==================== CONFIGURACIÓN DEL PIPELINE HTTP ====================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VIDA FIT API v1"));
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "kiosko",
    pattern: "kiosko",
    defaults: new { controller = "Kiosko", action = "Index" });

app.Run("http://localhost:5000");

static void OpenBrowser(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ⚠️  No se pudo abrir el navegador: {ex.Message}");
    }
}