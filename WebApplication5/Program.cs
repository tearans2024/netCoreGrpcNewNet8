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

builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();

// Kelas untuk menyimpan konfigurasi database
public class DatabaseConfig
{
    public string ConnectionString { get; set; }
}
