using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;
using Ziklo.Infrastructure;

internal static class DemoSeed
{
    public static readonly Guid UserId = Guid.Parse("9b7e2b94-60cc-47ee-9b58-a7652d0a2026");

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZikloDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Municipalities" (
                "Code" TEXT NOT NULL PRIMARY KEY, "Name" TEXT NOT NULL,
                "DepartmentCode" TEXT NOT NULL, "DepartmentName" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Municipalities_DepartmentCode_Name"
                ON "Municipalities" ("DepartmentCode", "Name");
            """);
        await MunicipalitySeed.SeedDemoAsync(db);
        // EnsureCreated does not add tables to an existing demonstration database.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CreditApplications" (
                "CompanyId" TEXT NOT NULL, "Id" TEXT NOT NULL, "CreatedAtUtc" TEXT NOT NULL,
                "Reference" TEXT NOT NULL, "ProductVersionId" TEXT NOT NULL,
                "IdentificationType" TEXT NOT NULL, "IdentificationNumber" TEXT NOT NULL,
                "ApplicantName" TEXT NOT NULL, "Email" TEXT NOT NULL, "Phone" TEXT NOT NULL,
                "City" TEXT NOT NULL, "EconomicActivity" TEXT NOT NULL,
                "MonthlyIncome" TEXT NOT NULL, "MonthlyObligations" TEXT NOT NULL,
                "RequestedAmount" TEXT NOT NULL, "RequestedInstallments" INTEGER NOT NULL,
                "Purpose" TEXT NOT NULL, "FormDataJson" TEXT NOT NULL DEFAULT '{{}}', "Status" TEXT NOT NULL,
                "AcknowledgementVersion" TEXT NOT NULL, "AcknowledgedAtUtc" TEXT NOT NULL,
                PRIMARY KEY ("CompanyId", "Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CreditApplications_CompanyId_Reference"
                ON "CreditApplications" ("CompanyId", "Reference");
            CREATE INDEX IF NOT EXISTS "IX_CreditApplications_CompanyId_CreatedAtUtc"
                ON "CreditApplications" ("CompanyId", "CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_CreditApplications_CompanyId_IdentificationNumber"
                ON "CreditApplications" ("CompanyId", "IdentificationNumber");
            CREATE TABLE IF NOT EXISTS "CreditApplicationDocuments" (
                "CompanyId" TEXT NOT NULL, "Id" TEXT NOT NULL, "CreatedAtUtc" TEXT NOT NULL,
                "ApplicationId" TEXT NOT NULL, "Kind" TEXT NOT NULL,
                "FileName" TEXT NOT NULL, "ContentType" TEXT NOT NULL,
                "Length" INTEGER NOT NULL, "Sha256" TEXT NOT NULL, "Content" BLOB NOT NULL,
                PRIMARY KEY ("CompanyId", "Id"),
                FOREIGN KEY ("CompanyId", "ApplicationId")
                    REFERENCES "CreditApplications" ("CompanyId", "Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CreditApplicationDocuments_CompanyId_ApplicationId_Kind"
                ON "CreditApplicationDocuments" ("CompanyId", "ApplicationId", "Kind");
            """);
        var columns = await db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM pragma_table_info('CreditApplications')").ToListAsync();
        if (!columns.Contains("FormDataJson"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"CreditApplications\" ADD COLUMN \"FormDataJson\" TEXT NOT NULL DEFAULT '{{}}'");
        if (!columns.Contains("LoanId"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"CreditApplications\" ADD COLUMN \"LoanId\" TEXT NULL");
        if (!columns.Contains("SubmittedByUserId"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"CreditApplications\" ADD COLUMN \"SubmittedByUserId\" TEXT NULL");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CreditApplications_CompanyId_LoanId"
                ON "CreditApplications" ("CompanyId", "LoanId")
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_CreditApplications_CompanyId_SubmittedByUserId_Status"
                ON "CreditApplications" ("CompanyId", "SubmittedByUserId", "Status")
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CreditApplicationReviews" (
                "CompanyId" TEXT NOT NULL, "Id" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL, "ApplicationId" TEXT NOT NULL,
                "ActorUserId" TEXT NOT NULL, "PreviousStatus" TEXT NOT NULL,
                "NewStatus" TEXT NOT NULL, "Notes" TEXT NOT NULL,
                "VerifiedMonthlyIncome" TEXT NULL,
                "VerifiedMonthlyObligations" TEXT NULL,
                "RecordedAtUtc" TEXT NOT NULL,
                PRIMARY KEY ("CompanyId", "Id"),
                FOREIGN KEY ("CompanyId", "ApplicationId")
                    REFERENCES "CreditApplications" ("CompanyId", "Id") ON DELETE RESTRICT,
                FOREIGN KEY ("ActorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_CreditApplicationReviews_CompanyId_ApplicationId_RecordedAtUtc"
                ON "CreditApplicationReviews" ("CompanyId", "ApplicationId", "RecordedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_CreditApplicationReviews_ActorUserId"
                ON "CreditApplicationReviews" ("ActorUserId");
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AccessProfiles" (
                "Id" TEXT NOT NULL PRIMARY KEY, "CompanyId" TEXT NOT NULL,
                "Name" TEXT NOT NULL, "PermissionMask" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL, "CreatedAtUtc" TEXT NOT NULL,
                FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccessProfiles_CompanyId_Name"
                ON "AccessProfiles" ("CompanyId", "Name");
            """);
        var membershipColumns = await db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM pragma_table_info('UserCompanies')").ToListAsync();
        if (!membershipColumns.Contains("ProfileId"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"UserCompanies\" ADD COLUMN \"ProfileId\" TEXT NULL");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_UserCompanies_ProfileId"
                ON "UserCompanies" ("ProfileId")
            """);
        var originalCompany = await db.Companies.SingleOrDefaultAsync(x =>
            x.Name == "Ziklo · Empresa de demostración" && x.TaxIdentifier == "DEMO-2026");
        if (originalCompany is not null)
        {
            originalCompany.Name = "Ziklo";
            originalCompany.TaxIdentifier = "PENDIENTE";
            await db.SaveChangesAsync();
        }
        if (await db.Companies.AnyAsync()) return;

        var company = new Company
        {
            Name = "Ziklo", TaxIdentifier = "PENDIENTE",
            CurrencyCode = "COP", TimeZoneId = "America/Bogota"
        };
        var user = new UserAccount
        {
            Id = UserId, Email = "demo@ziklo.local", PasswordHash = "DEMO_ACCESS_ONLY"
        };
        db.Companies.Add(company);
        db.Users.Add(user);
        db.UserCompanies.Add(new UserCompanyMembership
        {
            CompanyId = company.Id, UserId = user.Id, Role = InitialRole.Administrator
        });
        await db.SaveChangesAsync();

        DateOnly today = DailyCloseService.BusinessDate(company);
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var dailyClose = scope.ServiceProvider.GetRequiredService<IDailyCloseService>();
        async Task<Client> Client(string identification, string name, string city,
            string phone, decimal income) => await lending.CreateClientAsync(company.Id,
                new CreateClient("CC", identification, name,
                    $"Calle {identification[^2..]} # 10-20", phone, phone,
                    $"{name.Split(' ')[0].ToLowerInvariant()}@ejemplo.test", city,
                    null, "Comercio y servicios", income, null), default);
        var ana = await Client("1002345678", "Ana María Rojas", "Bogotá", "3005550101", 4500000m);
        var carlos = await Client("1023456789", "Carlos Andrés Gómez", "Medellín", "3005550102", 3200000m);
        var laura = await Client("1034567890", "Laura Catalina Pérez", "Cali", "3005550103", 6000000m);
        var martin = await Client("1045678901", "Martín Felipe Ruiz", "Bogotá", "3005550104", 2800000m);
        var daniela = await Client("1056789012", "Daniela Torres", "Barranquilla", "3005550105", 5100000m);
        await lending.CreateClientReferenceAsync(company.Id, ana.Id,
            new CreateClientReference(ClientReferenceKind.Personal, "Sofía Rojas",
                "Hermana", "3005550199", null, "Referencia de ejemplo"), default);

        var product = await lending.CreateProductAsync(company.Id,
            new CreateProduct("PERSONAL-DEMO", "Préstamo personal", "Producto de demostración"), default);
        var version = await lending.CreateProductVersionAsync(company.Id, product.Id,
            new CreateProductVersion(100000m, 50000000m, 1, 36, 0.018m,
                RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
                PaymentFrequency.Monthly, AmortizationMethod.French, 2, false,
                8000m, 0m, today.AddYears(-1), "COP", LateAnnualRate: 0.12m,
                LateGraceDays: 2), default);

        async Task<Loan> ActiveLoan(Client client, string number, decimal principal,
            int installments, DateOnly disbursement, DateOnly firstDue)
        {
            var loan = await lending.CreateLoanAsync(company.Id,
                new CreateLoan(client.Id, version.Id, number, disbursement,
                    principal, installments), default);
            await lending.RequestLoanAsync(company.Id, loan.Id, default);
            await lending.ApproveLoanAsync(company.Id, loan.Id,
                new ApproveLoan(disbursement), default);
            await lending.DisburseLoanAsync(company.Id, loan.Id,
                new DisburseLoan(disbursement, firstDue, $"demo-disburse-{number}"),
                user.Id, default);
            return loan;
        }

        var anaLoan = await ActiveLoan(ana, "ZK-2026-001", 6000000m, 12,
            today.AddMonths(-6), today.AddMonths(-5));
        var carlosLoan = await ActiveLoan(carlos, "ZK-2026-002", 3800000m, 9,
            today.AddMonths(-3), today.AddMonths(-2));
        await ActiveLoan(laura, "ZK-2026-003", 9500000m, 18,
            today.AddDays(-20), today.AddDays(10));
        var paidLoan = await ActiveLoan(daniela, "ZK-2026-005", 1200000m, 1,
            today.AddMonths(-3), today.AddMonths(-2));
        var rejected = await lending.CreateLoanAsync(company.Id,
            new CreateLoan(martin.Id, version.Id, "ZK-2026-004",
                today.AddDays(-15), 2500000m, 12), default);
        await lending.RequestLoanAsync(company.Id, rejected.Id, default);
        await lending.RejectLoanAsync(company.Id, rejected.Id,
            new RejectLoan(today.AddDays(-14), "Capacidad de pago insuficiente para el plazo solicitado."),
            user.Id, default);

        var firstTwo = await db.LoanInstallments.AsNoTracking()
            .Where(x => x.CompanyId == company.Id && x.LoanId == anaLoan.Id)
            .OrderBy(x => x.Number).Take(2).ToListAsync();
        await payments.ReceiveAsync(company.Id, anaLoan.Id,
            new ReceivePayment(firstTwo[0].DueDate, firstTwo[0].Payment,
                "Transferencia", "DEMO-REC-001", "demo-payment-001"), user.Id, default);
        await payments.ReceiveAsync(company.Id, anaLoan.Id,
            new ReceivePayment(firstTwo[1].DueDate, 180000m,
                "Efectivo", "DEMO-REC-002", "demo-payment-002"), user.Id, default);
        var paidLine = await db.LoanInstallments.AsNoTracking().SingleAsync(x =>
            x.CompanyId == company.Id && x.LoanId == paidLoan.Id);
        await payments.ReceiveAsync(company.Id, paidLoan.Id,
            new ReceivePayment(paidLine.DueDate, paidLine.Payment,
                "Transferencia", "DEMO-REC-003", "demo-payment-003"), user.Id, default);
        var collections = scope.ServiceProvider.GetRequiredService<ICollectionService>();
        await collections.CreateAsync(company.Id, carlosLoan.Id,
            new CreateCollectionActivity(today.AddDays(-1), CollectionChannel.Call,
                "Promesa de pago", "El cliente acordó realizar un abono mañana.",
                today, today, 500000m), user.Id, default);
        await dailyClose.RunAsync(company.Id, new RunDailyClose(today.AddDays(-1)), user.Id, default);
        await payments.ReceiveAsync(company.Id, anaLoan.Id,
            new ReceivePayment(today, 150000m,
                "PSE", "DEMO-REC-HOY", "demo-payment-today"), user.Id, default);
    }
}
