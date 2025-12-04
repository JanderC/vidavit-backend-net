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

var builder = WebApplication.CreateBuilder(args);

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

// PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=vidafit;Username=postgres;Password=postgres";

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
        Title = "VIDA FIT API",
        Version = "v1",
        Description = "Sistema de Gestión de Gimnasio con Check-in Biométrico"
    });
});

var app = builder.Build();

// ==================== INICIALIZACIÓN ====================

// Inicializar el servicio de huellas ANTES del banner
var fingerprintService = app.Services.GetRequiredService<IFingerprintService>();
fingerprintService.Initialize();

// Mostrar banner DESPUÉS de la inicialización
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
Console.WriteLine($"   ✓ Servidor iniciado en: http://localhost:5000");
Console.WriteLine($"   ✓ Base de datos: PostgreSQL - Conectada");
Console.WriteLine($"   {(fingerprintService.IsReaderConnected() ? "✓" : "✗")} Lector de huellas: {(fingerprintService.IsReaderConnected() ? "Conectado ✓" : "No detectado ✗")}");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   📱 Kiosko de Check-in: http://localhost:5000/kiosko");
Console.WriteLine("   🎛️  Panel Admin: http://localhost:5000/admin");
Console.WriteLine("   📊 API REST: http://localhost:5000/api");
Console.WriteLine("   📚 Swagger: http://localhost:5000/swagger");
Console.WriteLine("═══════════════════════════════════════════════════════");

// ==================== CONFIGURACIÓN DEL PIPELINE HTTP ====================

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VIDA FIT API v1"));
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

app.Run("http://localhost:5000");