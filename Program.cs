using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WorkTimePro.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVICES =================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database (SQLite)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ──────────────────────────────────────────────
// JWT Authentication – required for login to work without 500
// Crash protection: throw meaningful error if config is missing
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] 
    ?? throw new InvalidOperationException("JWT:Key is missing in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero // optional: no tolerance for clock difference
        };
    });

builder.Services.AddAuthorization();

// CORS (dev only – restrict in production!)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ================= MIDDLEWARE =================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();   // ← Shows real error instead of blank 500
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Temporary console logger for exceptions (very helpful during dev)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n╔════════════════════════════════════════════╗");
        Console.WriteLine($"║ REQUEST FAILED: {context.Request.Method} {context.Request.Path}");
        Console.WriteLine($"║ {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"║ Stack: {ex.StackTrace}");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");
        Console.ResetColor();

        throw; // still let dev page show full details
    }
});

app.UseStaticFiles();      // frontend files from wwwroot
app.UseCors("AllowAll");

app.UseAuthentication();   // MUST come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

// ================= DATABASE SEED =================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        DbSeeder.Seed(db);
        Console.WriteLine("Database seeded successfully.");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Seeding failed: " + ex.Message);
        Console.ResetColor();
        // You can throw; to crash on startup if seed is critical
    }
}

app.Run();