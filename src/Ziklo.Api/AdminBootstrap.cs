using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ziklo.Domain;
using Ziklo.Infrastructure;

internal static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Ziklo")))
            throw new InvalidOperationException("Configura ConnectionStrings__Ziklo antes de crear el administrador.");
        string email = Required(configuration, "ZIKLO_SEED_EMAIL").Trim().ToUpperInvariant();
        string password = Required(configuration, "ZIKLO_SEED_PASSWORD");
        string companyName = Required(configuration, "ZIKLO_SEED_COMPANY_NAME").Trim();
        string taxId = Required(configuration, "ZIKLO_SEED_TAX_ID").Trim();
        if (password.Length < 12)
            throw new InvalidOperationException("La contraseña inicial debe tener al menos 12 caracteres.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZikloDbContext>();
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.Users.AnyAsync())
            throw new InvalidOperationException("La instalación ya tiene usuarios; el aprovisionamiento inicial está cerrado.");
        if (await db.Companies.AnyAsync())
            throw new InvalidOperationException("La instalación ya tiene una empresa; Ziklo admite una sola.");

        var company = new Company { Name = companyName, TaxIdentifier = taxId };
        var admin = new UserAccount { Email = email };
        admin.PasswordHash = new PasswordHasher<UserAccount>().HashPassword(admin, password);
        db.Companies.Add(company);
        db.Users.Add(admin);
        db.UserCompanies.Add(new UserCompanyMembership
        {
            CompanyId = company.Id, UserId = admin.Id, Role = InitialRole.Administrator
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("Empresa y administrador inicial creados.");
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Falta la variable {key}.");
}
