using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ziklo.Api;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;
using Ziklo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
DotEnvConfiguration.Load(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<LoanSimulator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("simulation", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("credit-application", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 31 * 1024 * 1024);

string? connectionString = builder.Configuration.GetConnectionString("Ziklo");
string? jwtKey = builder.Configuration["ZIKLO_JWT_KEY"];
bool demoMode = builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("Ziklo:DemoMode");
if (demoMode && (!string.IsNullOrWhiteSpace(connectionString) ||
    !string.IsNullOrWhiteSpace(jwtKey)))
    throw new InvalidOperationException("El modo demo no puede combinarse con SQL Server ni JWT.");
if (!demoMode && (string.IsNullOrWhiteSpace(connectionString) ||
    string.IsNullOrWhiteSpace(jwtKey)))
    throw new InvalidOperationException(
        "Configura ConnectionStrings__Ziklo y ZIKLO_JWT_KEY antes de iniciar Ziklo.");
if (demoMode)
{
    string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "demo-data");
    Directory.CreateDirectory(dataDirectory);
    string keyFile = Path.Combine(dataDirectory, "local-auth.key");
    if (!File.Exists(keyFile))
        File.WriteAllBytes(keyFile, RandomNumberGenerator.GetBytes(32));
    jwtKey = Convert.ToBase64String(File.ReadAllBytes(keyFile));
}
if (demoMode)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}
bool liveMode = !demoMode && !string.IsNullOrWhiteSpace(connectionString) &&
    !string.IsNullOrWhiteSpace(jwtKey);
bool businessEnabled = demoMode || liveMode;
if (businessEnabled)
{
    if (demoMode)
    {
        string demoDirectory = Path.Combine(builder.Environment.ContentRootPath, "demo-data");
        Directory.CreateDirectory(demoDirectory);
        string keyDirectory = Path.Combine(demoDirectory, "keys");
        Directory.CreateDirectory(keyDirectory);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        string demoFile = Path.Combine(demoDirectory, "ziklo-demo.db");
        builder.Services.AddDbContext<ZikloDbContext>(options =>
            options.UseSqlite($"Data Source={demoFile}"));
    }
    else
        builder.Services.AddDbContext<ZikloDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<ILendingService, LendingService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IDailyCloseService, DailyCloseService>();
    builder.Services.AddScoped<IPortfolioService, PortfolioService>();
    builder.Services.AddScoped<ICollectionService, CollectionService>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IConsistencyService, ConsistencyService>();
    builder.Services.AddScoped<IStatementService, StatementService>();
    builder.Services.AddScoped<IClientDocumentService, ClientDocumentService>();
    builder.Services.AddScoped<IPaymentReportService, PaymentReportService>();
    builder.Services.AddScoped<IReportCatalogService, ReportCatalogService>();
    builder.Services.AddScoped<ICreditApplicationService, CreditApplicationService>();
}
if (businessEnabled)
{
    if (Encoding.UTF8.GetByteCount(jwtKey!) < 32)
        throw new InvalidOperationException("ZIKLO_JWT_KEY debe tener al menos 32 bytes UTF-8.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "Ziklo",
                ValidateAudience = true, ValidAudience = "Ziklo.Api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();
if (args.Contains("--seed-admin"))
{
    if (demoMode) throw new InvalidOperationException("El administrador real no se crea en modo demo.");
    await AdminBootstrap.RunAsync(app.Services, builder.Configuration);
    return;
}
if (demoMode) await DemoSeed.RunAsync(app.Services);
if (businessEnabled)
{
    using var scope = app.Services.CreateScope();
    var companyCount = await scope.ServiceProvider.GetRequiredService<ZikloDbContext>()
        .Companies.AsNoTracking().CountAsync();
    if (companyCount > 1)
        throw new InvalidOperationException("Ziklo admite una sola empresa por instalación.");
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
if (demoMode)
    app.Use(async (http, next) =>
    {
        if ((http.Request.Path.StartsWithSegments("/api/auth") ||
             http.Request.Path.StartsWithSegments("/api/companies")) &&
            (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address)))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    });
if (businessEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapPost("/api/simulations/loans", (SimulationRequest request, LoanSimulator simulator) =>
{
    try { return Results.Ok(simulator.Simulate(request)); }
    catch (FinancialCalculationException error)
    {
        return Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest,
            title: "Datos financieros no válidos");
    }
}).RequireRateLimiting("simulation");

if (demoMode)
{
    app.MapGet("/api/demo/application-products", async (HttpContext http,
        ZikloDbContext db, CancellationToken ct) =>
    {
        if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
            return Results.NotFound();
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var products = await db.ProductVersions.AsNoTracking()
            .Where(v => v.IsPublished && v.ValidFrom <= today)
            .Join(db.CreditProducts.AsNoTracking().Where(p => p.IsActive),
                version => new { version.CompanyId, Id = version.ProductId },
                product => new { product.CompanyId, product.Id },
                (version, product) => new
                {
                    companyId = version.CompanyId, productVersionId = version.Id,
                    productName = product.Name, version.MinimumAmount, version.MaximumAmount,
                    version.MinimumInstallments, version.MaximumInstallments,
                    version.CurrencyCode
                }).OrderBy(x => x.productName).ToListAsync(ct);
        return Results.Ok(products);
    }).WithTags("Solicitudes de demostración");

    app.MapPost("/api/demo/credit-applications", async (HttpContext http,
        ICreditApplicationService service, CancellationToken ct) =>
    {
        if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
            return Results.NotFound();
        if (http.Request.Headers.Authorization.Count > 0 &&
            http.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();
        if (!http.Request.HasFormContentType)
            return Results.Problem("Envía el formulario con multipart/form-data.", statusCode: 415);
        if (http.Request.ContentLength > 31 * 1024 * 1024)
            return Results.Problem("La solicitud supera el tamaño permitido.", statusCode: 413);
        try
        {
            var form = await http.Request.ReadFormAsync(ct);
            string Value(string key) => form[key].ToString();
            if (!Guid.TryParse(Value("companyId"), out Guid companyId) ||
                !Guid.TryParse(Value("productVersionId"), out Guid productVersionId) ||
                !decimal.TryParse(Value("monthlyIncome"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal income) ||
                !decimal.TryParse(Value("monthlyObligations"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal obligations) ||
                !decimal.TryParse(Value("requestedAmount"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal amount) ||
                !int.TryParse(Value("requestedInstallments"), out int installments))
                return Results.Problem("Revisa los valores numéricos de la solicitud.", statusCode: 400);
            if (form.Files.Count > CreditApplicationService.MaximumDocuments)
                return Results.Problem("Adjunta máximo tres soportes.", statusCode: 400);
            var uploads = new List<CreditApplicationUpload>();
            foreach (var file in form.Files)
            {
                if (file.Length > CreditApplicationService.MaximumDocumentBytes)
                    return Results.Problem("Cada soporte puede tener máximo 10 MB.", statusCode: 413);
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);
                uploads.Add(new CreditApplicationUpload(file.Name, file.FileName,
                    file.ContentType, buffer.ToArray()));
            }
            var request = new SubmitCreditApplication(companyId, productVersionId,
                Value("identificationType"), Value("identificationNumber"),
                Value("applicantName"), Value("email"), Value("phone"),
                Value("city"), Value("economicActivity"), income, obligations,
                amount, installments, Value("purpose"), Value("formDataJson"),
                Value("demoAcknowledged") == "true", uploads,
                Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid submittedBy)
                    ? submittedBy : null);
            var receipt = await service.SubmitAsync(request, ct);
            return Results.Created("/solicitar.html", receipt);
        }
        catch (LendingRuleException error)
        {
            return Results.Problem(error.Message, statusCode: 400, title: "Solicitud no válida");
        }
        catch (InvalidDataException)
        {
            return Results.Problem("El formulario supera el tamaño permitido.", statusCode: 413);
        }
        catch (DbUpdateException)
        {
            return Results.Problem("No se pudo radicar la solicitud. Intenta nuevamente.", statusCode: 409);
        }
    }).RequireRateLimiting("credit-application")
      .WithTags("Solicitudes de demostración");

    app.MapGet("/api/demo/credit-applications/{reference}", async (string reference,
        HttpContext http, ZikloDbContext db, CancellationToken ct) =>
    {
        if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
            return Results.NotFound();
        var application = await db.CreditApplications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Reference == reference, ct);
        if (application is null) return Results.NotFound();
        var company = await db.Companies.AsNoTracking()
            .SingleAsync(x => x.Id == application.CompanyId, ct);
        return Results.Ok(new { application.Reference, application.CreatedAtUtc,
            application.IdentificationType, application.IdentificationNumber,
            application.ApplicantName, application.Email, application.Phone,
            application.City, application.EconomicActivity, application.MonthlyIncome,
            application.MonthlyObligations, application.RequestedAmount,
            application.RequestedInstallments, application.Purpose,
            application.FormDataJson, company.Name, company.TaxIdentifier });
    }).WithTags("Solicitudes de demostración");
}

if (businessEnabled)
{
    if (demoMode)
    {
        app.MapGet("/api/auth/setup-status", async (HttpContext http, ZikloDbContext db,
            CancellationToken ct) =>
        {
            if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
                return Results.NotFound();
            bool required = await db.Users.AnyAsync(x => x.Id == DemoSeed.UserId &&
                x.PasswordHash == "DEMO_ACCESS_ONLY", ct);
            return Results.Ok(new { required });
        }).WithTags("Autenticación");
        app.MapPost("/api/auth/setup", async (SetupRequest request, HttpContext http,
            ZikloDbContext db, CancellationToken ct) =>
        {
            if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
                return Results.NotFound();
            string email = request.Email?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) ||
                parsed.Address.ToUpperInvariant() != email || email.Length > 256 ||
                request.Password is null || request.Password.Length < 12 ||
                request.Password.Length > 200)
                return Results.Problem("Indica un correo válido y una contraseña de 12 a 200 caracteres.",
                    statusCode: 400);
            if (await db.Users.AnyAsync(x => x.Email == email && x.Id != DemoSeed.UserId, ct))
                return Results.Problem("Ese correo ya está en uso.", statusCode: 409);
            var initialAdmin = new UserAccount { Id = DemoSeed.UserId, Email = email };
            string hash = new PasswordHasher<UserAccount>().HashPassword(initialAdmin, request.Password);
            int changed = await db.Users.Where(x => x.Id == DemoSeed.UserId &&
                x.PasswordHash == "DEMO_ACCESS_ONLY")
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.Email, email)
                    .SetProperty(x => x.PasswordHash, hash), ct);
            if (changed == 0)
                return Results.Problem("El administrador inicial ya fue configurado.", statusCode: 409);
            return Results.NoContent();
        }).RequireRateLimiting("login").WithTags("Autenticación");
    }
    app.MapPost("/api/auth/login", async (LoginRequest request, ZikloDbContext db) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Results.BadRequest();
        string email = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.IsActive);
        var hasher = new PasswordHasher<UserAccount>();
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("Ziklo", "Ziklo.Api",
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(60), signingCredentials: credentials);
        return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresInSeconds = 3600 });
    }).RequireRateLimiting("login").WithTags("Autenticación");
    app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, ZikloDbContext db,
        CancellationToken ct) =>
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == userId && x.IsActive, ct);
        if (user is null) return Results.Unauthorized();
        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Join(db.Companies.AsNoTracking().Where(x => x.IsActive),
                membership => membership.CompanyId, company => company.Id,
                (membership, company) => new
                {
                    companyId = company.Id, company.Name,
                    membership.Role, membership.ProfileId,
                    profileName = db.AccessProfiles.Where(p => p.Id == membership.ProfileId &&
                        p.CompanyId == company.Id).Select(p => p.Name).FirstOrDefault(),
                    company.CurrencyCode
                }).ToListAsync(ct);
        return Results.Ok(new { user.Id, user.Email, companies = memberships });
    }).RequireAuthorization().WithTags("Autenticación");
    LendingEndpoints.Map(app);
}

app.Run();

public sealed record LoginRequest(string Email, string Password);
public sealed record SetupRequest(string Email, string Password);
