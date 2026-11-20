using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// --- Base de datos -------------------------------------------------------------------------------------------------
builder.Services.AddDbContext<NexoDbContext>(o => o.UseSqlServer(config.GetConnectionString("Nexo")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Nexo.")));

// --- Autenticación -------------------------------------------------------------------------------------------------
var jwt = config.GetSection("Jwt").Get<JwtOpciones>() ?? new JwtOpciones();
if (Encoding.UTF8.GetByteCount(jwt.Clave) < 32)
    throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 caracteres. Configúrala en appsettings.Development.json, user-secrets o una variable de entorno.");
builder.Services.Configure<JwtOpciones>(config.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Emisor,
        ValidateAudience = true, ValidAudience = jwt.Audiencia,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
    };
    // El token solo vale si la cuenta sigue activa y no cambió su sello (contraseña, rol o estado).
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<NexoDbContext>();
            var sub = ctx.Principal?.FindFirstValue("sub");
            var u = int.TryParse(sub, out var id)
                ? await db.Usuarios.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Activo, x.Sello, x.DebeCambiarPassword }).FirstOrDefaultAsync()
                : null;
            if (u is null || !u.Activo || u.Sello != ctx.Principal!.FindFirstValue(Claves.Sello)) { ctx.Fail("Sesión no válida."); return; }
            if (u.DebeCambiarPassword) ((ClaimsIdentity)ctx.Principal!.Identity!).AddClaim(new Claim(Claves.PasswordTemporal, "1"));
        },
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Administrador, p => p.RequireRole("Administrador"))
    .AddPolicy(Politicas.Gestion, p => p.RequireRole("Administrador", "Supervisor"));

// Máximo 10 intentos de inicio de sesión por minuto por IP (además del bloqueo de la cuenta tras 5 fallos).
var loginPorMinuto = config.GetValue("Seguridad:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

// --- Web -----------------------------------------------------------------------------------------------------------
var origenes = config.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Auditoria>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexo RH API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Token de POST /api/auth/login",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

// Crea o actualiza la base y carga los datos de ejemplo (solo si está habilitado; por defecto en Development).
if (config.GetValue("BaseDatos:MigrarAlIniciar", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
    await db.Database.MigrateAsync();
    await Semilla.CargarAsync(db, config, app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
// Con contraseña temporal solo se puede usar /api/auth (para cambiarla).
app.Use(async (ctx, next) =>
{
    if (ctx.User.HasClaim(Claves.PasswordTemporal, "1") && !ctx.Request.Path.StartsWithSegments("/api/auth"))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { title = "Debes cambiar tu contraseña temporal antes de continuar.", status = 403, codigo = "password_temporal" });
        return;
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" })).AllowAnonymous();

app.Run();

public partial class Program;
