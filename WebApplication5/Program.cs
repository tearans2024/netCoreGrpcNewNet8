using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Konfigurasi logging
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
}).CreateLogger("Program");

string settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "settings.txt");
Dictionary<string, string> settings = new();

// Baca file settings.txt
if (File.Exists(settingsFilePath))
{
    var lines = File.ReadAllLines(settingsFilePath);
    foreach (var line in lines)
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            settings[parts[0].Trim()] = parts[1].Trim();
        }
    }
}
else
{
    logger.LogError("File settings.txt tidak ditemukan!");
    throw new FileNotFoundException("File settings.txt tidak ditemukan!", settingsFilePath);
}

// Ambil koneksi database dari settings.txt
if (!settings.TryGetValue("DB_CONNECTION", out string dbConnection) || string.IsNullOrWhiteSpace(dbConnection))
{
    logger.LogError("Connection string tidak ditemukan di settings.txt!");
    throw new Exception("Connection string tidak ditemukan di file settings.txt.");
}

logger.LogInformation($"Menggunakan database: {dbConnection}");

// Simpan konfigurasi database dalam Dependency Injection (DI)
builder.Services.Configure<DatabaseConfig>(options =>
{
    options.ConnectionString = dbConnection;
});

// Tambahkan DbContext untuk PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dbConnection));

// Tambahkan autentikasi JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddAuthorization();

// Tambahkan Swagger jika diaktifkan
if (builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    });
}

builder.Services.AddControllers();

var app = builder.Build();

// Middleware untuk batasan ukuran request
app.Use(async (context, next) =>
{
    context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = 5242880; // 5 MB
    await next();
});

// Konfigurasi Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000); // Mendengarkan pada semua alamat IP di port 5000
});

// Aktifkan Swagger UI jika diaktifkan
if (app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1"));
}

// Aktifkan logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Aktifkan autentikasi dan otorisasi
app.UseAuthentication();
app.UseAuthorization();

// Mapping controllers
app.MapControllers();

app.Run();

// Kelas untuk menyimpan konfigurasi database
public class DatabaseConfig
{
    public string ConnectionString { get; set; }
}

// Kelas DbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Tambahkan DbSet untuk model Anda
    // Contoh: public DbSet<Product> Products { get; set; }
}