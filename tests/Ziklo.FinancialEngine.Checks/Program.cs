using Ziklo.FinancialEngine;
using Ziklo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Ziklo.Application;
using Ziklo.Domain;
using System.IO.Compression;
using System.Xml.Linq;

var simulator = new LoanSimulator();
int passed = 0;

void Check(string name, Action assertion)
{
    assertion();
    Console.WriteLine($"OK {name}");
    passed++;
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Esperado {expected}; obtenido {actual}.");
}

SimulationRequest Base(AmortizationMethod method = AmortizationMethod.French) =>
    new(1000m, 0.01m, RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
        PaymentFrequency.Monthly, 2, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), method);

Check("cuota fija y residuo final", () =>
{
    var result = simulator.Simulate(Base());
    Equal(507.51m, result.Schedule[0].Payment);
    Equal(10.00m, result.Schedule[0].Interest);
    Equal(497.51m, result.Schedule[0].Principal);
    Equal(502.49m, result.Schedule[1].Principal);
    Equal(5.02m, result.Schedule[1].Interest);
    Equal(1015.02m, result.TotalPayable);
    Equal(0m, result.Schedule[^1].ClosingPrincipal);
});

Check("abono fijo a capital", () =>
{
    var result = simulator.Simulate(Base(AmortizationMethod.FixedPrincipal));
    Equal(510.00m, result.Schedule[0].Payment);
    Equal(505.00m, result.Schedule[1].Payment);
    Equal(1000m, result.TotalPrincipalRepaid);
});

Check("interés simple con 30 días ACT/360", () =>
{
    Equal(10m, new InterestEngine().CalculateInterest(1000m, 0.12m,
        RateKind.AnnualSimple, DayCountConvention.Actual360, PaymentFrequency.Monthly,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    var request = new SimulationRequest(1000m, 0.12m, RateKind.AnnualSimple,
        DayCountConvention.Actual360, PaymentFrequency.Daily, 30,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), AmortizationMethod.Bullet);
    var result = simulator.Simulate(request);
    Equal(new DateOnly(2026, 1, 31), result.FinalDueDate);
    Equal(10.00m, result.TotalInterest);
    Equal(1010.00m, result.TotalPayable);
});

Check("capitalización explícita y pago único", () =>
{
    var request = Base(AmortizationMethod.Bullet) with { CapitalizeInterest = true };
    var result = simulator.Simulate(request);
    Equal(10.00m, result.Schedule[0].CapitalizedInterest);
    Equal(0m, result.Schedule[0].Payment);
    Equal(10.10m, result.Schedule[1].Interest);
    Equal(1020.10m, result.TotalPayable);
    Equal(20.10m, result.TotalInterest);
});

Check("pago único sin capitalización", () =>
{
    var result = simulator.Simulate(Base(AmortizationMethod.Bullet));
    Equal(20.00m, result.TotalInterest);
    Equal(1020.00m, result.TotalPayable);
    Equal(1, result.Schedule.Count);
});

Check("capital personalizado", () =>
{
    var request = Base(AmortizationMethod.Custom) with { CustomPrincipal = new[] { 400m, 600m } };
    var result = simulator.Simulate(request);
    Equal(410m, result.Schedule[0].Payment);
    Equal(606m, result.Schedule[1].Payment);
    Equal(1016m, result.TotalPayable);
});

Check("tasa cero", () =>
{
    var result = simulator.Simulate(Base() with { Rate = 0 });
    Equal(500m, result.Schedule[0].Payment);
    Equal(1000m, result.TotalPayable);
});

Check("año bisiesto y cruce de año", () =>
{
    Equal(1m / 366m, LoanSimulator.YearFraction(new DateOnly(2024, 2, 28),
        new DateOnly(2024, 2, 29), DayCountConvention.ActualActual));
    decimal expected = 1m / 366m + 1m / 365m;
    Equal(expected, LoanSimulator.YearFraction(new DateOnly(2024, 12, 31),
        new DateOnly(2025, 1, 2), DayCountConvention.ActualActual));
});

Check("vencimientos de fin de mes", () =>
{
    var request = Base() with
    {
        DisbursementDate = new DateOnly(2026, 1, 31),
        FirstDueDate = new DateOnly(2026, 2, 28)
    };
    var result = simulator.Simulate(request);
    Equal(new DateOnly(2026, 3, 31), result.Schedule[1].DueDate);
});

Check("tasa efectiva anual", () =>
{
    var request = new SimulationRequest(1000m, 0.12m, RateKind.EffectiveAnnual,
        DayCountConvention.Actual365Fixed, PaymentFrequency.Annual, 1,
        new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), AmortizationMethod.Bullet);
    Equal(120m, simulator.Simulate(request).TotalInterest);
});

Check("rechaza capital personalizado inconsistente", () =>
{
    try
    {
        simulator.Simulate(Base(AmortizationMethod.Custom) with { CustomPrincipal = new[] { 400m, 500m } });
        throw new Exception("Se aceptó un plan inválido.");
    }
    catch (FinancialCalculationException) { }
});

Check("pago parcial por prioridad", () =>
{
    var engine = new PaymentEngine();
    var due = new DateOnly(2026, 1, 1);
    var buckets = new[]
    {
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.Principal, due.AddMonths(1), 825m),
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.OverduePrincipal, due, 100m),
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.OverdueCurrentInterest, due, 30m),
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.LateInterest, due, 10m),
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.Charges, due, 20m),
        new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.CurrentInterest, due.AddMonths(1), 15m)
    };
    var result = engine.ApplyPayment(120m, buckets, PaymentEngine.DefaultPolicy);
    Equal(120m, result.Applied);
    Equal(0m, result.Credit);
    Equal(4, result.Allocations.Count);
    Equal(PaymentCategory.Charges, result.Allocations[0].Category);
    Equal(20m, result.Allocations[0].Amount);
    Equal(60m, result.Allocations[3].Amount);
    Equal(40m, result.RemainingBuckets.Single(x => x.Category == PaymentCategory.OverduePrincipal).Outstanding);
});

Check("múltiples cuotas vencidas en orden", () =>
{
    var engine = new PaymentEngine();
    var older = Guid.NewGuid();
    var newer = Guid.NewGuid();
    var result = engine.ApplyPayment(75m,
        [new PaymentBucket(newer, null, PaymentCategory.OverduePrincipal, new DateOnly(2026, 2, 1), 50m),
         new PaymentBucket(older, null, PaymentCategory.OverduePrincipal, new DateOnly(2026, 1, 1), 50m)],
        PaymentEngine.DefaultPolicy);
    Equal(older, result.Allocations[0].BucketId);
    Equal(50m, result.Allocations[0].Amount);
    Equal(newer, result.Allocations[1].BucketId);
    Equal(25m, result.Allocations[1].Amount);
});

Check("excedente registrado como saldo a favor", () =>
{
    var engine = new PaymentEngine();
    var debt = new PaymentBucket(Guid.NewGuid(), null, PaymentCategory.Principal,
        new DateOnly(2026, 1, 1), 100m);
    var result = engine.ApplyPayment(120m, [debt], PaymentEngine.DefaultPolicy);
    Equal(100m, result.Applied);
    Equal(20m, result.Credit);
    try
    {
        engine.ApplyPayment(120m, [debt], PaymentEngine.DefaultPolicy with
        { ExcessHandling = ExcessHandling.Reject });
        throw new Exception("Se aceptó un excedente prohibido.");
    }
    catch (FinancialCalculationException) { }
});

Check("liquidación anticipada desglosada", () =>
{
    var result = new PaymentEngine().CalculatePayoff(new PayoffComponents(
        900m, 20m, 2m, 5m, 1m, 3m, 10m));
    Equal(921m, result.Total);
});

Check("mora diaria y pago exacto en vencimiento", () =>
{
    var engine = new LateInterestEngine();
    var due = new DateOnly(2026, 1, 1);
    var points = new[] { new LateBasePoint(due, 100m) };
    var rule = new LateInterestRule(0.24m, 0, GraceMode.Prospective,
        DayCountConvention.Actual360);
    Equal(0m, engine.CalculateLateInterest(due, due, points, rule).CumulativeInterest);
    var result = engine.CalculateLateInterest(due, due.AddDays(5), points, rule);
    Equal(5, result.EligibleDays);
    Equal(0.33m, result.CumulativeInterest);
    Equal(0.23m, engine.CalculateLateInterest(due, due.AddDays(5), points,
        rule, alreadyPosted: 0.10m).IncrementToPost);
});

Check("mora con base reducida por pago parcial", () =>
{
    var due = new DateOnly(2026, 1, 1);
    var result = new LateInterestEngine().CalculateLateInterest(due, due.AddDays(5),
        [new LateBasePoint(due, 100m), new LateBasePoint(due.AddDays(3), 50m)],
        new LateInterestRule(0.24m, 0, GraceMode.Prospective, DayCountConvention.Actual360));
    Equal(2, result.Segments.Count);
    Equal(0.27m, result.CumulativeInterest);
});

Check("gracia prospectiva, retroactiva y tope", () =>
{
    var due = new DateOnly(2026, 1, 1);
    var engine = new LateInterestEngine();
    var points = new[] { new LateBasePoint(due, 100m) };
    var prospective = new LateInterestRule(0.24m, 2, GraceMode.Prospective,
        DayCountConvention.Actual360);
    Equal(0m, engine.CalculateLateInterest(due, due.AddDays(2), points, prospective).CumulativeInterest);
    Equal(0.20m, engine.CalculateLateInterest(due, due.AddDays(5), points, prospective).CumulativeInterest);
    var retroactive = prospective with { GraceMode = GraceMode.Retroactive, MaximumAmount = 0.25m };
    Equal(0.25m, engine.CalculateLateInterest(due, due.AddDays(5), points, retroactive).CumulativeInterest);
});

Check("saldo reconstruido desde movimientos", () =>
{
    var balance = new BalanceEngine().CalculateOutstandingBalance(
        [new FinancialDelta(1, FinancialComponent.Principal, 1000m),
         new FinancialDelta(2, FinancialComponent.CurrentInterest, 10m),
         new FinancialDelta(3, FinancialComponent.CurrentInterest, -10m),
         new FinancialDelta(4, FinancialComponent.Principal, -100m),
         new FinancialDelta(5, FinancialComponent.Credit, 5m)]);
    Equal(900m, balance.Principal);
    Equal(0m, balance.CurrentInterest);
    Equal(895m, balance.AmountDue);
    Equal(5L, balance.LastSequence);
});

Check("libro rechaza secuencias faltantes", () =>
{
    try
    {
        new BalanceEngine().CalculateOutstandingBalance(
            [new FinancialDelta(1, FinancialComponent.Principal, 100m),
             new FinancialDelta(3, FinancialComponent.Principal, -10m)]);
        throw new Exception("Se aceptó una secuencia incompleta.");
    }
    catch (FinancialCalculationException) { }
});

Check("modelo EF multiempresa válido", () =>
{
    var options = new DbContextOptionsBuilder<ZikloDbContext>()
        .UseSqlServer("Server=localhost;Database=ZikloModelCheck;Trusted_Connection=True;TrustServerCertificate=True")
        .Options;
    using var context = new ZikloDbContext(options);
    if (context.Model.FindEntityType(typeof(CreditApplication)) is null ||
        context.Model.FindEntityType(typeof(CreditApplicationDocument)) is null ||
        context.Model.FindEntityType(typeof(Loan)) is null ||
        context.Model.FindEntityType(typeof(PaymentReceipt)) is null)
        throw new Exception("El modelo EF no contiene todas las entidades previstas.");
});

Check("cliente, producto, aprobación y desembolso aislados por empresa", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Empresa de prueba", TaxIdentifier = "TEST-1" };
    var otherCompany = new Company { Name = "Otra empresa", TaxIdentifier = "TEST-2" };
    var user = new UserAccount { Email = "TEST@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.AddRange(company, otherCompany);
    context.Users.Add(user);
    context.UserCompanies.Add(new UserCompanyMembership
    {
        CompanyId = company.Id, UserId = user.Id, Role = InitialRole.Administrator
    });
    context.SaveChanges();
    var service = new LendingService(context, simulator);
    var client = service.CreateClientAsync(company.Id,
        new CreateClient("CC", "123", "Cliente de prueba", null, null, null, null,
            null, null, null, null, null), default).GetAwaiter().GetResult();
    var reference = service.CreateClientReferenceAsync(company.Id, client.Id,
        new CreateClientReference(ClientReferenceKind.Personal, "Contacto", "Familiar",
            "3001234567", null, null), default).GetAwaiter().GetResult();
    Equal(client.Id, reference.ClientId);
    Equal(1, context.ClientReferences.Count());
    Equal(1, context.AuditEntries.Count(x => x.EntityName == nameof(ClientReference)));
    var documentService = new ClientDocumentService(context);
    byte[] pdf = "%PDF-1.7 test"u8.ToArray();
    var document = documentService.UploadAsync(company.Id, client.Id, "identificacion.pdf",
        "application/pdf", pdf, user.Id, default).GetAwaiter().GetResult();
    Equal(pdf.Length, document.Length);
    Equal(1, context.AuditEntries.Count(x => x.EntityName == nameof(ClientDocument)));
    Equal(1, documentService.ListAsync(company.Id, client.Id, default)
        .GetAwaiter().GetResult()!.Count);
    Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)), document.Sha256);
    Equal(pdf.Length, documentService.GetAsync(company.Id, client.Id, document.Id, default)
        .GetAwaiter().GetResult()!.Content.Length);
    Equal(null, documentService.GetAsync(otherCompany.Id, client.Id, document.Id, default)
        .GetAwaiter().GetResult());
    try
    {
        documentService.UploadAsync(company.Id, client.Id, "falso.pdf", "application/pdf",
            "not a PDF"u8.ToArray(), user.Id, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó un archivo con firma inválida.");
    }
    catch (LendingRuleException) { }
    try
    {
        service.CreateClientReferenceAsync(otherCompany.Id, client.Id,
            new CreateClientReference(ClientReferenceKind.Commercial, "Empresa", null,
                null, null, null), default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó una referencia de otra empresa.");
    }
    catch (LendingRuleException) { }
    var product = service.CreateProductAsync(company.Id,
        new CreateProduct("PERSONAL", "Préstamo personal", null), default).GetAwaiter().GetResult();
    var version = service.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 24, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.French, 2, false, 0, 0,
            DateOnly.FromDateTime(DateTime.UtcNow), "COP"), default)
        .GetAwaiter().GetResult();
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var loan = service.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "P-001", today, 1000m, 2), default)
        .GetAwaiter().GetResult();
    service.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    service.ApproveLoanAsync(company.Id, loan.Id, new ApproveLoan(today), default)
        .GetAwaiter().GetResult();
    service.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(today, today.AddMonths(1), "test-disbursement-1"), user.Id, default)
        .GetAwaiter().GetResult();
    Equal(LoanStatus.Active, loan.Status);
    Equal(2, context.LoanInstallments.Count());
    Equal(1000m, context.LoanMovements.Single().Amount);
    Equal(1, context.LoanEvents.Count());
    Equal(1, context.OutboxMessages.Count(x => x.EventType == "LoanDisbursed"));
    var reconstructed = new BalanceEngine().CalculateOutstandingBalance(context.LoanMovements
        .OrderBy(x => x.Sequence)
        .Select(x => new FinancialDelta(x.Sequence, FinancialComponent.Principal, x.Amount))
        .ToList());
    Equal(1000m, reconstructed.AmountDue);
    Equal(0, context.Loans.Count(x => x.CompanyId == otherCompany.Id));
    var rejected = service.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "P-REJECTED", today, 500m, 2), default)
        .GetAwaiter().GetResult();
    service.RequestLoanAsync(company.Id, rejected.Id, default).GetAwaiter().GetResult();
    service.RejectLoanAsync(company.Id, rejected.Id,
        new RejectLoan(today, "Capacidad de pago insuficiente"), user.Id, default)
        .GetAwaiter().GetResult();
    Equal(LoanStatus.Rejected, rejected.Status);
    Equal(1, context.LoanRejections.Count());
    Equal(1, context.AuditEntries.Count(x => x.EntityName == nameof(LoanRejection)));
    try
    {
        service.ApproveLoanAsync(company.Id, rejected.Id, new ApproveLoan(today), default)
            .GetAwaiter().GetResult();
        throw new Exception("Se aprobó un préstamo rechazado.");
    }
    catch (LendingRuleException) { }
    try
    {
        service.CreateLoanAsync(otherCompany.Id,
            new CreateLoan(client.Id, version.Id, "CROSS", today, 1000m, 2), default)
            .GetAwaiter().GetResult();
        throw new Exception("Se aceptó un cliente de otra empresa.");
    }
    catch (LendingRuleException) { }
    service.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(today, today.AddMonths(1), "test-disbursement-1"), user.Id, default)
        .GetAwaiter().GetResult();
    Equal(1, context.LoanEvents.Count());
    var movement = context.LoanMovements.Single();
    movement.Amount = 1m;
    try
    {
        context.SaveChanges();
        throw new Exception("Se permitió modificar el libro financiero.");
    }
    catch (InvalidOperationException) { }
    context.ChangeTracker.Clear();
    var publishedVersion = context.ProductVersions.Single();
    publishedVersion.Rate = 0.02m;
    try
    {
        context.SaveChanges();
        throw new Exception("Se permitió modificar una versión publicada.");
    }
    catch (InvalidOperationException) { }
});

Check("prioridad contractual copiada y aplicada al recaudo", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Prioridad", TaxIdentifier = "PRIORITY-1" };
    var user = new UserAccount { Email = "PRIORITY@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var client = lending.CreateClientAsync(company.Id,
        new CreateClient("CC", "PRIORITY-CLIENT", "Cliente", null, null, null, null,
            null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("PRIORITY", "Producto", null), default).GetAwaiter().GetResult();
    var priority = new[] { PaymentCategory.Principal, PaymentCategory.CurrentInterest,
        PaymentCategory.Charges, PaymentCategory.LateInterest,
        PaymentCategory.OverdueCurrentInterest, PaymentCategory.OverduePrincipal };
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.French, 2, false, 0, 0,
            new DateOnly(2026, 1, 1), "COP", PaymentPriority: priority), default)
        .GetAwaiter().GetResult();
    var loan = lending.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "PRIORITY-LOAN", new DateOnly(2026, 1, 1), 1000m, 2),
        default).GetAwaiter().GetResult();
    lending.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    lending.ApproveLoanAsync(company.Id, loan.Id, new ApproveLoan(new DateOnly(2026, 1, 1)), default)
        .GetAwaiter().GetResult();
    Equal(version.PaymentPriority, context.LoanTerms.Single().PaymentPriority);
    lending.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "priority-disburse"),
        user.Id, default).GetAwaiter().GetResult();
    var receipt = new PaymentService(context).ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 2, 1), 5m, "Transferencia", "PRIORITY-REF", "priority-pay"),
        user.Id, default).GetAwaiter().GetResult();
    Equal(1, receipt.Allocations.Count);
    Equal(BalanceComponent.Principal, receipt.Allocations[0].Component);
    Equal(5m, receipt.Allocations[0].Amount);
});

Check("recaudo parcial persistido e idempotente", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Recaudos", TaxIdentifier = "PAY-1" };
    var user = new UserAccount { Email = "PAY@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var service = new LendingService(context, simulator);
    var client = service.CreateClientAsync(company.Id,
        new CreateClient("CC", "PAY-CLIENT", "Cliente de recaudo", null, null, null, null,
            null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = service.CreateProductAsync(company.Id,
        new CreateProduct("PAY", "Producto recaudo", null), default).GetAwaiter().GetResult();
    var version = service.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.French, 2, false, 0, 0,
            new DateOnly(2026, 1, 1), "COP"), default).GetAwaiter().GetResult();
    var loan = service.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "PAY-LOAN", new DateOnly(2026, 1, 1), 1000m, 2),
        default).GetAwaiter().GetResult();
    service.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    service.ApproveLoanAsync(company.Id, loan.Id, new ApproveLoan(new DateOnly(2026, 1, 1)), default)
        .GetAwaiter().GetResult();
    service.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "pay-disburse"),
        user.Id, default).GetAwaiter().GetResult();
    var paymentService = new PaymentService(context);
    var first = new ReceivePayment(new DateOnly(2026, 2, 1), 15m, "Transferencia", "REF-1", "pay-1");
    var receipt = paymentService.ReceiveAsync(company.Id, loan.Id, first, user.Id, default)
        .GetAwaiter().GetResult();
    Equal(15m, receipt.Applied);
    Equal(2, receipt.Allocations.Count);
    Equal(BalanceComponent.CurrentInterest, receipt.Allocations[0].Component);
    Equal(10m, receipt.Allocations[0].Amount);
    Equal(BalanceComponent.Principal, receipt.Allocations[1].Component);
    Equal(5m, receipt.Allocations[1].Amount);
    var retry = paymentService.ReceiveAsync(company.Id, loan.Id, first, user.Id, default)
        .GetAwaiter().GetResult();
    Equal(receipt.Id, retry.Id);
    Equal(receipt.Id, paymentService.GetAsync(company.Id, loan.Id, receipt.Id, default)
        .GetAwaiter().GetResult()!.Id);
    var report = new PaymentReportService(context).GetAsync(company.Id,
        new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), default)
        .GetAwaiter().GetResult();
    Equal(1, report.Count);
    Equal(15m, report.TotalReceived);
    Equal(15m, report.ByMethod.Single().Received);
    Equal("PAY-LOAN", report.Rows.Single().LoanNumber);
    var catalog = new ReportCatalogService(context, new PortfolioService(context),
        new PaymentReportService(context));
    var paymentTable = catalog.GetAsync(company.Id, "payments",
        new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), default)
        .GetAwaiter().GetResult();
    Equal(1, paymentTable.Rows.Count);
    Equal("15.00", paymentTable.Rows[0][6]);
    using (var spreadsheet = new ZipArchive(new MemoryStream(ReportSpreadsheet.Create(paymentTable)),
        ZipArchiveMode.Read))
        if (spreadsheet.GetEntry("xl/worksheets/sheet1.xml") is null)
            throw new Exception("El informe Excel no contiene su hoja.");
    Equal<PaymentReceiptView?>(null, paymentService.GetAsync(Guid.NewGuid(), loan.Id,
        receipt.Id, default).GetAwaiter().GetResult());
    Equal(1, context.PaymentReceipts.Count());
    var second = paymentService.ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 2, 1), 492.51m, "Transferencia", "REF-2", "pay-2"),
        user.Id, default).GetAwaiter().GetResult();
    Equal(492.51m, second.Applied);
    Equal(1, second.Allocations.Count);
    Equal(1, context.LoanEvents.Count(x => x.Type == LoanEventType.CurrentInterestAccrual));
    Equal(502.49m, context.LoanMovements.Where(x => x.Component == BalanceComponent.Principal)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(0m, context.LoanMovements.Where(x => x.Component == BalanceComponent.CurrentInterest)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(2, context.OutboxMessages.Count(x => x.EventType == "PaymentReceived"));
    try
    {
        paymentService.ReceiveAsync(company.Id, loan.Id,
            new ReceivePayment(new DateOnly(2026, 2, 1), 1m, "Transferencia", "REF-3", "pay-3"),
            user.Id, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó un recaudo sin deuda exigible.");
    }
    catch (LendingRuleException) { }
    Equal(2, context.PaymentReceipts.Count());
    var final = paymentService.ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 3, 1), 507.51m, "Transferencia", "REF-4", "pay-4"),
        user.Id, default).GetAwaiter().GetResult();
    Equal(507.51m, final.Applied);
    Equal(LoanStatus.PaidOff, context.Loans.Single(x => x.Id == loan.Id).Status);
    Equal(3, context.OutboxMessages.Count(x => x.EventType == "PaymentReceived"));
    Equal(0m, context.LoanMovements.Where(x => x.Component == BalanceComponent.Principal)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(0m, context.LoanMovements.Where(x => x.Component == BalanceComponent.CurrentInterest)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(0, new ConsistencyService(context).CheckAsync(company.Id, default)
        .GetAwaiter().GetResult().IssueCount);
    var firstMovement = context.LoanMovements.AsNoTracking().OrderBy(x => x.Sequence).First();
    context.Database.ExecuteSqlInterpolated($"UPDATE LoanMovements SET Sequence = {99L} WHERE CompanyId = {company.Id} AND Id = {firstMovement.Id}");
    if (!new ConsistencyService(context).CheckAsync(company.Id, default)
        .GetAwaiter().GetResult().Issues.Any(x => x.Code == "LEDGER"))
        throw new Exception("La conciliación no detectó la secuencia rota.");
});

Check("recaudo aplicado solo a las cuotas escogidas", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Selección de cuotas", TaxIdentifier = "PAY-SELECT" };
    var user = new UserAccount { Email = "SELECT@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var client = lending.CreateClientAsync(company.Id,
        new CreateClient("CC", "SELECT-CLIENT", "Cliente de cuotas", null, null, null,
            null, null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("SELECT", "Producto de cuotas", null), default).GetAwaiter().GetResult();
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.French, 2, false, 0, 0,
            new DateOnly(2026, 1, 1), "COP"), default).GetAwaiter().GetResult();
    var loan = lending.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "SELECT-LOAN", new DateOnly(2026, 1, 1),
            900m, 3), default).GetAwaiter().GetResult();
    lending.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    lending.ApproveLoanAsync(company.Id, loan.Id,
        new ApproveLoan(new DateOnly(2026, 1, 1)), default).GetAwaiter().GetResult();
    lending.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1),
            "select-disburse"), user.Id, default).GetAwaiter().GetResult();
    var installments = context.LoanInstallments.AsNoTracking()
        .Where(x => x.LoanId == loan.Id).OrderBy(x => x.Number).ToList();
    var payments = new PaymentService(context);
    var receipt = payments.ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 3, 1), 5m, "Efectivo", "SELECT-REF",
            "select-payment", [2]), user.Id, default).GetAwaiter().GetResult();
    if (receipt.Allocations.Count == 0 ||
        receipt.Allocations.Any(x => x.InstallmentId != installments[1].Id))
        throw new Exception("El recaudo se aplicó a una cuota no seleccionada.");
    Equal(LoanStatus.InArrears, context.Loans.Single(x => x.Id == loan.Id).Status);
    try
    {
        payments.ReceiveAsync(company.Id, loan.Id,
            new ReceivePayment(new DateOnly(2026, 3, 1), 1m, "Efectivo", "FUTURE-REF",
                "future-payment", [3]), user.Id, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó una cuota que todavía no es exigible.");
    }
    catch (LendingRuleException) { }
    Equal(1, context.PaymentReceipts.Count());
    var combined = payments.ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 3, 1),
            installments[0].Payment + installments[1].Payment - 5m,
            "Efectivo", "TWO-REF", "two-payment", [1, 2]),
        user.Id, default).GetAwaiter().GetResult();
    if (!combined.Allocations.Any(x => x.InstallmentId == installments[0].Id) ||
        !combined.Allocations.Any(x => x.InstallmentId == installments[1].Id) ||
        combined.Allocations.Any(x => x.InstallmentId == installments[2].Id))
        throw new Exception("El recaudo de dos cuotas no coincide con la selección.");
    Equal(LoanStatus.Active, context.Loans.Single(x => x.Id == loan.Id).Status);
});

Check("cierre diario y mora persistida sin duplicados", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Cierre", TaxIdentifier = "CLOSE-1" };
    var user = new UserAccount { Email = "CLOSE@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var client = lending.CreateClientAsync(company.Id,
        new CreateClient("CC", "CLOSE-CLIENT", "Cliente de cierre", null, null, null, null,
            null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("CLOSE", "Producto cierre", null), default).GetAwaiter().GetResult();
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual360,
            PaymentFrequency.Monthly, AmortizationMethod.Bullet, 2, false, 0, 0,
            new DateOnly(2026, 1, 1), "COP", 0.24m), default).GetAwaiter().GetResult();
    var loan = lending.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "CLOSE-LOAN", new DateOnly(2026, 1, 1), 1000m, 1),
        default).GetAwaiter().GetResult();
    lending.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    lending.ApproveLoanAsync(company.Id, loan.Id,
        new ApproveLoan(new DateOnly(2026, 1, 1)), default).GetAwaiter().GetResult();
    lending.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "close-disburse"),
        user.Id, default).GetAwaiter().GetResult();
    var closing = new DailyCloseService(context);
    var dueClose = closing.RunAsync(company.Id, new RunDailyClose(new DateOnly(2026, 2, 1)),
        user.Id, default).GetAwaiter().GetResult();
    Equal(1, dueClose.AccrualEvents);
    Equal(0, dueClose.LateEvents);
    var lateClose = closing.RunAsync(company.Id, new RunDailyClose(new DateOnly(2026, 2, 6)),
        user.Id, default).GetAwaiter().GetResult();
    Equal(1, lateClose.LateEvents);
    Equal(lateClose.Id, closing.RunAsync(company.Id,
        new RunDailyClose(new DateOnly(2026, 2, 6)), user.Id, default).GetAwaiter().GetResult().Id);
    Equal(2, context.OutboxMessages.Count(x => x.EventType == "DailyCloseCompleted"));
    Equal(3.33m, context.LoanMovements.Where(x => x.Component == BalanceComponent.LateInterest)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(LoanStatus.InArrears, context.Loans.Single(x => x.Id == loan.Id).Status);
    var portfolio = new PortfolioService(context).GetAsync(company.Id,
        new DateOnly(2026, 2, 6), default).GetAwaiter().GetResult();
    Equal(1, portfolio.DelinquentLoanCount);
    Equal(5, portfolio.Loans.Single().DaysPastDue);
    Equal("1-30", portfolio.Loans.Single().AgingBucket);
    Equal(1013.33m, portfolio.Total);
    byte[] spreadsheet = PortfolioSpreadsheet.Create(portfolio);
    string? fixturePath = Environment.GetEnvironmentVariable("ZIKLO_XLSX_FIXTURE");
    if (!string.IsNullOrWhiteSpace(fixturePath)) File.WriteAllBytes(fixturePath, spreadsheet);
    using (var archive = new ZipArchive(new MemoryStream(spreadsheet),
               ZipArchiveMode.Read))
    {
        if (archive.GetEntry("[Content_Types].xml") is null ||
            archive.GetEntry("xl/workbook.xml") is null ||
            archive.GetEntry("xl/worksheets/sheet1.xml") is not { } sheet)
            throw new Exception("El Excel no contiene las partes requeridas.");
        var document = XDocument.Load(sheet.Open());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Equal("1013.33", document.Descendants(ns + "c")
            .Single(x => (string?)x.Attribute("r") == "L2").Element(ns + "v")!.Value);
    }
    var statement = new StatementService(context, new PortfolioService(context)).GetAsync(
        company.Id, loan.Id, new DateOnly(2026, 2, 6), default).GetAwaiter().GetResult()!;
    Equal(1, statement.PastDueInstallments);
    Equal(5, statement.DaysPastDue);
    Equal(1013.33m, statement.TotalOutstanding);
    var dashboard = new DashboardService(context, new PortfolioService(context)).GetAsync(
        company.Id, new DateOnly(2026, 2, 6), default).GetAwaiter().GetResult();
    Equal(1000m, dashboard.CapitalDisbursed);
    Equal(1013.33m, dashboard.PortfolioTotal);
    Equal(1, dashboard.DelinquentLoanCount);
    var receipt = new PaymentService(context).ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 2, 7), 1014m,
            "Transferencia", "CLOSE-REF", "close-payment"), user.Id, default)
        .GetAwaiter().GetResult();
    Equal(4m, receipt.Allocations.Single(x => x.Component == BalanceComponent.LateInterest).Amount);
    Equal(LoanStatus.PaidOff, context.Loans.Single(x => x.Id == loan.Id).Status);
    Equal(2, context.LateInterestAssessments.Count());
    Equal(0m, context.LoanMovements.Where(x => x.Component == BalanceComponent.LateInterest)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(0, new PortfolioService(context).GetAsync(company.Id,
        new DateOnly(2026, 2, 7), default).GetAwaiter().GetResult().LoanCount);
    var finalDashboard = new DashboardService(context, new PortfolioService(context)).GetAsync(
        company.Id, new DateOnly(2026, 2, 7), default).GetAwaiter().GetResult();
    Equal(1014m, finalDashboard.ReceiptsToday);
    Equal(10m, finalDashboard.CurrentInterestCollected);
    Equal(4m, finalDashboard.LateInterestCollected);
    Equal(1, finalDashboard.PaidOffLoanCount);
    var finalStatement = new StatementService(context, new PortfolioService(context)).GetAsync(
        company.Id, loan.Id, new DateOnly(2026, 2, 7), default).GetAwaiter().GetResult()!;
    Equal(0m, finalStatement.TotalOutstanding);
    Equal(1000m, finalStatement.PrincipalPaid);
    Equal(10m, finalStatement.CurrentInterestPaid);
    Equal(4m, finalStatement.LateInterestPaid);
    Equal(1, finalStatement.Receipts.Count);
    var clientSummary = new PortfolioService(context).GetClientAsync(company.Id,
        client.Id, new DateOnly(2026, 2, 7), default).GetAwaiter().GetResult()!;
    Equal(1, clientSummary.PaidOffLoanCount);
    Equal(0, clientSummary.ActiveLoanCount);
    Equal(1014m, clientSummary.TotalReceived);
});

Check("promesa parcial evaluada por cierre", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
    var company = new Company { Name = "Cobranza", TaxIdentifier = "COLLECT-1" };
    var user = new UserAccount { Email = "COLLECT@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var client = lending.CreateClientAsync(company.Id,
        new CreateClient("CC", "COLLECT-CLIENT", "Cliente cobranza", null, null, null, null,
            null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("COLLECT", "Producto cobranza", null), default).GetAwaiter().GetResult();
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.Bullet, 2, false, 0, 0,
            today.AddDays(-40), "COP"), default).GetAwaiter().GetResult();
    var loan = lending.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "COLLECT-LOAN", today.AddDays(-40), 1000m, 1),
        default).GetAwaiter().GetResult();
    lending.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    lending.ApproveLoanAsync(company.Id, loan.Id,
        new ApproveLoan(today.AddDays(-40)), default).GetAwaiter().GetResult();
    lending.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(today.AddDays(-40), today.AddDays(-10), "collect-disburse"),
        user.Id, default).GetAwaiter().GetResult();
    var collection = new CollectionService(context);
    var activity = collection.CreateAsync(company.Id, loan.Id,
        new CreateCollectionActivity(today.AddDays(-1), CollectionChannel.Call,
            "Acuerdo", "Promesa parcial", null, today, 100m), user.Id, default)
        .GetAwaiter().GetResult();
    if (activity.PromiseId is null) throw new Exception("No se creó la promesa.");
    try
    {
        collection.CreateAsync(company.Id, loan.Id,
            new CreateCollectionActivity(today, CollectionChannel.WhatsApp,
                "Nuevo acuerdo", "Promesa duplicada", null, today.AddDays(1), 50m),
            user.Id, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó una segunda promesa pendiente.");
    }
    catch (LendingRuleException) { }
    new PaymentService(context).ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(today, 50m, "Transferencia", "COLLECT-REF", "collect-payment"),
        user.Id, default).GetAwaiter().GetResult();
    var close = new DailyCloseService(context).RunAsync(company.Id,
        new RunDailyClose(today), user.Id, default).GetAwaiter().GetResult();
    Equal(1, close.PromisesEvaluated);
    Equal(PromiseStatus.Partial, context.PaymentPromises.Single().Status);
    Equal(50m, context.PaymentPromises.Single().ReceivedAmount);
    Equal(1, context.PromiseEvaluations.Count());
    Equal(close.Id, new DailyCloseService(context).RunAsync(company.Id,
        new RunDailyClose(today), user.Id, default).GetAwaiter().GetResult().Id);
    Equal(1, context.PromiseEvaluations.Count());
    Equal(PromiseStatus.Partial, collection.ListAsync(company.Id, loan.Id, default)
        .GetAwaiter().GetResult().Single().PromiseStatus);
});

Check("capitalización pactada registrada y cobrada", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Capitalización", TaxIdentifier = "CAP-1" };
    var user = new UserAccount { Email = "CAP@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var client = lending.CreateClientAsync(company.Id,
        new CreateClient("CC", "CAP-CLIENT", "Cliente capitalización", null, null, null,
            null, null, null, null, null, null), default).GetAwaiter().GetResult();
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("CAP", "Producto capitalización", null), default)
        .GetAwaiter().GetResult();
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100m, 10000m, 1, 12, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.Bullet, 2, true, 0, 0,
            new DateOnly(2026, 1, 1), "COP"), default).GetAwaiter().GetResult();
    var loan = lending.CreateLoanAsync(company.Id,
        new CreateLoan(client.Id, version.Id, "CAP-LOAN", new DateOnly(2026, 1, 1),
            1000m, 2, true), default).GetAwaiter().GetResult();
    lending.RequestLoanAsync(company.Id, loan.Id, default).GetAwaiter().GetResult();
    lending.ApproveLoanAsync(company.Id, loan.Id,
        new ApproveLoan(new DateOnly(2026, 1, 1)), default).GetAwaiter().GetResult();
    lending.DisburseLoanAsync(company.Id, loan.Id,
        new DisburseLoan(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "cap-disburse"),
        user.Id, default).GetAwaiter().GetResult();
    var closing = new DailyCloseService(context);
    closing.RunAsync(company.Id, new RunDailyClose(new DateOnly(2026, 2, 1)),
        user.Id, default).GetAwaiter().GetResult();
    Equal(1, context.LoanEvents.Count(x => x.Type == LoanEventType.Capitalization));
    Equal(1010m, context.LoanMovements.Where(x => x.Component == BalanceComponent.Principal)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(0m, context.LoanMovements.Where(x => x.Component == BalanceComponent.CurrentInterest)
        .AsEnumerable().Sum(x => x.Amount));
    Equal(1010m, new PortfolioService(context).GetAsync(company.Id,
        new DateOnly(2026, 2, 1), default).GetAwaiter().GetResult().Total);
    closing.RunAsync(company.Id, new RunDailyClose(new DateOnly(2026, 3, 1)),
        user.Id, default).GetAwaiter().GetResult();
    var receipt = new PaymentService(context).ReceiveAsync(company.Id, loan.Id,
        new ReceivePayment(new DateOnly(2026, 3, 2), 1020.10m,
            "Transferencia", "CAP-REF", "cap-payment"), user.Id, default)
        .GetAwaiter().GetResult();
    Equal(1020.10m, receipt.Applied);
    Equal(LoanStatus.PaidOff, context.Loans.Single(x => x.Id == loan.Id).Status);
    Equal(0, new ConsistencyService(context).CheckAsync(company.Id, default)
        .GetAwaiter().GetResult().IssueCount);
});

Check("permisos por empresa con historial", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Permisos", TaxIdentifier = "PERM-1" };
    var other = new Company { Name = "Otra", TaxIdentifier = "PERM-2" };
    var user = new UserAccount { Email = "PERM@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Companies.AddRange(company, other);
    context.Users.Add(user);
    context.SaveChanges();
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.Collector, BusinessPermission.RegisterPayment));
    Equal(true, PermissionPolicy.IsAllowed(InitialRole.Administrator,
        BusinessPermission.ManagePermissions));
    Equal(true, PermissionPolicy.IsAllowed(InitialRole.Auditor,
        BusinessPermission.ViewAudit));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.ReadOnly,
        BusinessPermission.ViewAudit));
    Equal(true, PermissionPolicy.IsAllowed(InitialRole.Originator,
        BusinessPermission.CreateLoan));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.Originator,
        BusinessPermission.StudyApplications,
        new Dictionary<BusinessPermission, bool> { [BusinessPermission.StudyApplications] = true }));
    Equal(true, PermissionPolicy.IsAllowed(InitialRole.CreditAnalyst,
        BusinessPermission.StudyApplications));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.CreditApprover,
        BusinessPermission.StudyApplications));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.Originator,
        BusinessPermission.ApproveLoan,
        new Dictionary<BusinessPermission, bool> { [BusinessPermission.ApproveLoan] = true }));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.CreditAnalyst,
        BusinessPermission.ApproveLoan));
    Equal(true, PermissionPolicy.IsAllowed(InitialRole.CreditApprover,
        BusinessPermission.ApproveLoan));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.CreditApprover,
        BusinessPermission.DisburseLoan));
    Equal(false, PermissionPolicy.IsAllowed(InitialRole.Manager,
        BusinessPermission.ManagePermissions,
        new Dictionary<BusinessPermission, bool> { [BusinessPermission.ManagePermissions] = true }));
    var service = new PermissionService(context);
    var changed = service.SetAsync(company.Id, InitialRole.Collector,
        BusinessPermission.RegisterPayment, true, user.Id, "127.0.0.1", default)
        .GetAwaiter().GetResult();
    Equal(true, changed.Allowed);
    Equal(1, context.PermissionChanges.Count());
    if (!context.AuditEntries.Any(x => x.EntityName == nameof(CompanyRolePermission)))
        throw new Exception("No se auditó el cambio de permisos.");
    service.SetAsync(company.Id, InitialRole.Collector,
        BusinessPermission.RegisterPayment, true, user.Id, "127.0.0.1", default)
        .GetAwaiter().GetResult();
    Equal(1, context.PermissionChanges.Count());
    Equal(true, service.ListAsync(company.Id, default).GetAwaiter().GetResult()
        .Single(x => x.Role == InitialRole.Collector &&
            x.Permission == BusinessPermission.RegisterPayment).Allowed);
    Equal(false, service.ListAsync(other.Id, default).GetAwaiter().GetResult()
        .Single(x => x.Role == InitialRole.Collector &&
            x.Permission == BusinessPermission.RegisterPayment).Allowed);
    try
    {
        service.SetAsync(company.Id, InitialRole.Administrator,
            BusinessPermission.ManagePermissions, false, user.Id, "127.0.0.1", default)
            .GetAwaiter().GetResult();
        throw new Exception("Se revocó el permiso principal.");
    }
    catch (LendingRuleException) { }
    try
    {
        service.SetAsync(company.Id, InitialRole.Originator,
            BusinessPermission.ApproveLoan, true, user.Id, "127.0.0.1", default)
            .GetAwaiter().GetResult();
        throw new Exception("Se concedió aprobación al perfil de radicación.");
    }
    catch (LendingRuleException) { }
});

Check("perfil propio conserva permisos aislados", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Perfil", TaxIdentifier = "PROFILE-1" };
    var user = new UserAccount { Email = "PROFILE@EXAMPLE.COM", PasswordHash = "test-only" };
    long mask = AccessProfilePolicy.CreateMask([
        BusinessPermission.ViewLoans, BusinessPermission.ApproveLoan]);
    var profile = new AccessProfile
    {
        CompanyId = company.Id, Name = "Comité de crédito", PermissionMask = mask
    };
    context.Companies.Add(company);
    context.Users.Add(user);
    context.AccessProfiles.Add(profile);
    context.UserCompanies.Add(new UserCompanyMembership
    {
        CompanyId = company.Id, UserId = user.Id,
        Role = InitialRole.ReadOnly, ProfileId = profile.Id
    });
    context.SaveChanges();
    Equal(true, AccessProfilePolicy.IsAllowed(profile.PermissionMask,
        BusinessPermission.ApproveLoan));
    Equal(false, AccessProfilePolicy.IsAllowed(profile.PermissionMask,
        BusinessPermission.DisburseLoan));
    Equal(profile.Id, context.UserCompanies.Single().ProfileId);
    var assignedName = context.UserCompanies
        .Where(membership => membership.CompanyId == company.Id)
        .Join(context.Users, membership => membership.UserId, account => account.Id,
            (membership, account) => context.AccessProfiles
                .Where(candidate => candidate.Id == membership.ProfileId)
                .Select(candidate => candidate.Name).FirstOrDefault())
        .Single();
    Equal(profile.Name, assignedName);
    try
    {
        AccessProfilePolicy.CreateMask([BusinessPermission.ManagePermissions]);
        throw new Exception("El perfil propio obtuvo administración de usuarios.");
    }
    catch (ArgumentException) { }
});

Check("solicitud radicada con soporte sin crear préstamo", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Originación", TaxIdentifier = "ORI-1" };
    context.Companies.Add(company);
    var originator = new UserAccount { Email = "RADICADOR@EXAMPLE.COM", PasswordHash = "test-only" };
    context.Users.Add(originator);
    context.UserCompanies.Add(new UserCompanyMembership
    {
        CompanyId = company.Id, UserId = originator.Id, Role = InitialRole.Originator
    });
    context.SaveChanges();
    var lending = new LendingService(context, simulator);
    var product = lending.CreateProductAsync(company.Id,
        new CreateProduct("PERSONAL", "Crédito personal", null), default)
        .GetAwaiter().GetResult();
    var version = lending.CreateProductVersionAsync(company.Id, product.Id,
        new CreateProductVersion(100000m, 10000000m, 1, 24, 0.01m,
            RateKind.PeriodicEffective, DayCountConvention.Actual365Fixed,
            PaymentFrequency.Monthly, AmortizationMethod.French, 2, false, 0, 0,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), "COP"), default)
        .GetAwaiter().GetResult();
    byte[] pdf = "%PDF-1.7 soporte ficticio"u8.ToArray();
    var request = new SubmitCreditApplication(company.Id, version.Id, "CC", "1098765432",
        "Valentina Martínez López", "valentina@ejemplo.test", "3005550140",
        "Bogotá", "Comerciante independiente", 4200000m, 750000m,
        5000000m, 12, "Compra de inventario de ejemplo", "{}", true,
        [new CreditApplicationUpload("Income", "ingresos.pdf", "application/pdf", pdf)],
        originator.Id);
    var service = new CreditApplicationService(context, lending);
    var receipt = service.SubmitAsync(request, default).GetAwaiter().GetResult();
    if (!receipt.Reference.StartsWith("SOL-")) throw new Exception("Radicado inválido.");
    Equal(CreditApplicationStatus.Submitted, receipt.Status);
    Equal(1, receipt.DocumentCount);
    Equal(1, context.CreditApplications.Count());
    Equal(originator.Id, context.CreditApplications.Single().SubmittedByUserId);
    Equal(1, context.CreditApplicationDocuments.Count());
    Equal(0, context.Loans.Count());
    Equal(0, context.LoanMovements.Count());
    Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)),
        context.CreditApplicationDocuments.Single().Sha256);
    try
    {
        service.SubmitAsync(request with { DemoAcknowledged = false }, default)
            .GetAwaiter().GetResult();
        throw new Exception("Se aceptó una solicitud sin confirmación de demo.");
    }
    catch (LendingRuleException) { }
    try
    {
        service.SubmitAsync(request with { Documents =
            [new CreditApplicationUpload("Income", "ingresos.pdf", "application/pdf",
                "archivo falso"u8.ToArray())] }, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó un soporte inválido.");
    }
    catch (LendingRuleException) { }
    Equal(1, context.CreditApplications.Count());
});

Check("estudio de solicitud conserva historial y bloquea decisiones inválidas", () =>
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<ZikloDbContext>().UseSqlite(connection).Options;
    using var context = new ZikloDbContext(options);
    context.Database.EnsureCreated();
    var company = new Company { Name = "Estudio", TaxIdentifier = "EST-1" };
    var user = new UserAccount { Email = "ANALISTA@EXAMPLE.COM", PasswordHash = "test-only" };
    var originator = new UserAccount { Email = "RADICADOR@EXAMPLE.COM", PasswordHash = "test-only" };
    var product = new CreditProduct { CompanyId = company.Id, Code = "PERSONAL", Name = "Personal" };
    var version = new ProductVersion { CompanyId = company.Id, ProductId = product.Id,
        Number = 1, IsPublished = true, CurrencyCode = "COP", MinimumAmount = 100m,
        MaximumAmount = 10000000m, MinimumInstallments = 1, MaximumInstallments = 12,
        Rate = 0.01m, RateKind = RateKind.PeriodicEffective,
        DayCount = DayCountConvention.Actual365Fixed,
        Frequency = PaymentFrequency.Monthly, Method = AmortizationMethod.French,
        ValidFrom = new DateOnly(2026, 1, 1) };
    var application = new CreditApplication { CompanyId = company.Id,
        ProductVersionId = version.Id, Reference = "SOL-TEST-REVIEW",
        SubmittedByUserId = originator.Id,
        IdentificationType = "CC", IdentificationNumber = "1098765432",
        ApplicantName = "Cliente de prueba", Email = "cliente@example.com",
        Phone = "3005550101", City = "Bogotá", EconomicActivity = "Comercio",
        MonthlyIncome = 3000000m, MonthlyObligations = 500000m,
        RequestedAmount = 2000000m, RequestedInstallments = 6,
        Purpose = "Capital de trabajo", AcknowledgementVersion = "DEMO-2026-01" };
    context.AddRange(company, user, originator, product, version, application);
    context.SaveChanges();
    var service = new CreditApplicationService(context, new LendingService(context, simulator));
    void Review(CreditApplicationStatus status, string notes, decimal? income = null,
        decimal? obligations = null) => service.ReviewAsync(company.Id, application.Id,
            user.Id, new RecordCreditApplicationReview(status, notes, income, obligations),
            default).GetAwaiter().GetResult();
    try { Review(CreditApplicationStatus.Approved, "Sin estudio", 3000000m, 500000m);
        throw new Exception("Se aprobó sin iniciar estudio."); }
    catch (LendingRuleException) { }
    using var staleContext = new ZikloDbContext(options);
    var stale = staleContext.CreditApplications.Single(x => x.Id == application.Id);
    Review(CreditApplicationStatus.InReview, "Analista asignado");
    stale.Status = CreditApplicationStatus.Rejected;
    try { staleContext.SaveChanges();
        throw new Exception("Se perdió el control de concurrencia del estado."); }
    catch (DbUpdateConcurrencyException) { }
    Review(CreditApplicationStatus.NeedsInformation, "Falta soporte de ingresos actualizado.");
    Equal(1, context.CreditApplications.Count(x => x.CompanyId == company.Id &&
        x.SubmittedByUserId == originator.Id &&
        x.Status == CreditApplicationStatus.NeedsInformation));
    Equal(0, context.CreditApplications.Count(x => x.CompanyId == company.Id &&
        x.SubmittedByUserId == user.Id &&
        x.Status == CreditApplicationStatus.NeedsInformation));
    var alertNote = context.CreditApplications.AsNoTracking()
        .Where(x => x.CompanyId == company.Id && x.SubmittedByUserId == originator.Id &&
            x.Status == CreditApplicationStatus.NeedsInformation)
        .Select(x => context.CreditApplicationReviews.AsNoTracking()
            .Where(review => review.CompanyId == company.Id && review.ApplicationId == x.Id &&
                review.NewStatus == CreditApplicationStatus.NeedsInformation)
            .OrderByDescending(review => review.RecordedAtUtc)
            .Select(review => review.Notes).FirstOrDefault()).Single();
    Equal("Falta soporte de ingresos actualizado.", alertNote);
    try { service.RespondToInformationAsync(company.Id, application.Id, user.Id,
        "Intento de otro usuario", default).GetAwaiter().GetResult();
        throw new Exception("Otro usuario respondió por el radicador."); }
    catch (LendingRuleException) { }
    byte[] additionalPdf = "%PDF-1.7 ingreso actualizado"u8.ToArray();
    var supplement = service.AttachSupplementAsync(company.Id, application.Id,
        originator.Id, "ingresos-actualizados.pdf", additionalPdf, default)
        .GetAwaiter().GetResult();
    if (!supplement.Kind.StartsWith("Sup-")) throw new Exception("Soporte adicional mal clasificado.");
    service.RespondToInformationAsync(company.Id, application.Id, originator.Id,
        "Adjunté el soporte de ingresos actualizado.", default).GetAwaiter().GetResult();
    Equal(CreditApplicationStatus.InReview, application.Status);
    Equal(0, context.CreditApplications.Count(x => x.CompanyId == company.Id &&
        x.SubmittedByUserId == originator.Id &&
        x.Status == CreditApplicationStatus.NeedsInformation));
    try { Review(CreditApplicationStatus.Approved, "", null, null);
        throw new Exception("Se aprobó sin cifras verificadas."); }
    catch (LendingRuleException) { }
    Review(CreditApplicationStatus.Approved, "Capacidad verificada", 3200000m, 500000m);
    Equal(CreditApplicationStatus.Approved, application.Status);
    Equal(4, context.CreditApplicationReviews.Count());
    Equal(3200000m, context.CreditApplicationReviews.OrderBy(x => x.RecordedAtUtc)
        .Last().VerifiedMonthlyIncome);
    try { Review(CreditApplicationStatus.Rejected, "Decisión posterior inválida");
        throw new Exception("Se cambió una decisión final."); }
    catch (LendingRuleException) { }
    Equal(4, context.CreditApplicationReviews.Count());
    Equal(0, context.Loans.Count());
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var linked = service.ConvertApprovedAsync(company.Id, application.Id, today, default)
        .GetAwaiter().GetResult();
    Equal(LoanStatus.Approved, linked.Status);
    Equal(linked.Id, application.LoanId);
    Equal(1, context.Clients.Count());
    Equal(1, context.Loans.Count());
    try { service.ConvertApprovedAsync(company.Id, application.Id, today, default)
        .GetAwaiter().GetResult();
        throw new Exception("Se convirtió dos veces la solicitud."); }
    catch (LendingRuleException) { }
    try { new LendingService(context, simulator).DisburseLoanAsync(company.Id, linked.Id,
        new DisburseLoan(today, today.AddMonths(1), "application-test"), user.Id,
        default).GetAwaiter().GetResult();
        throw new Exception("Se desembolsó sin aceptación contractual."); }
    catch (LendingRuleException) { }
    Equal(CreditApplicationStatus.Approved, application.Status);
    Equal(0, context.LoanEvents.Count(x => x.Type == LoanEventType.Disbursement));
    byte[] signedPdf = "%PDF-1.7 contrato firmado de prueba"u8.ToArray();
    try { service.AttachSignedContractAsync(company.Id, application.Id,
        "contrato.txt", signedPdf, default).GetAwaiter().GetResult();
        throw new Exception("Se aceptó un contrato que no es PDF."); }
    catch (LendingRuleException) { }
    var contract = service.AttachSignedContractAsync(company.Id, application.Id,
        "contrato-firmado.pdf", signedPdf, default).GetAwaiter().GetResult();
    Equal("SignedContract", contract.Kind);
    Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(signedPdf)),
        contract.Sha256);
    try { service.AttachSignedContractAsync(company.Id, application.Id,
        "otro.pdf", signedPdf, default).GetAwaiter().GetResult();
        throw new Exception("Se reemplazó un contrato firmado."); }
    catch (LendingRuleException) { }
    var disbursement = new DisburseLoan(today, today.AddMonths(1), "application-test");
    var active = new LendingService(context, simulator).DisburseLoanAsync(
        company.Id, linked.Id, disbursement, user.Id, default).GetAwaiter().GetResult();
    Equal(LoanStatus.Active, active.Status);
    Equal(CreditApplicationStatus.Disbursed, application.Status);
    Equal(1, context.LoanEvents.Count(x => x.LoanId == linked.Id &&
        x.Type == LoanEventType.Disbursement));
    Equal(6, context.LoanInstallments.Count(x => x.LoanId == linked.Id));
    new LendingService(context, simulator).DisburseLoanAsync(company.Id, linked.Id,
        disbursement, user.Id, default).GetAwaiter().GetResult();
    Equal(1, context.LoanEvents.Count(x => x.LoanId == linked.Id &&
        x.Type == LoanEventType.Disbursement));
    try { new LendingService(context, simulator).DisburseLoanAsync(company.Id,
        linked.Id, disbursement with { FirstDueDate = today.AddMonths(2) }, user.Id,
        default).GetAwaiter().GetResult();
        throw new Exception("Se reutilizó la clave de desembolso con otra fecha."); }
    catch (LendingRuleException) { }
});

Console.WriteLine($"{passed} comprobaciones correctas.");
