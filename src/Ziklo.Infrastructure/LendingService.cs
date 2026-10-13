using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;

namespace Ziklo.Infrastructure;

public sealed class LendingService(ZikloDbContext db, LoanSimulator simulator) : ILendingService
{
    public async Task<Client> CreateClientAsync(Guid companyId, CreateClient command, CancellationToken ct)
    {
        await RequireCompany(companyId, ct);
        if (string.IsNullOrWhiteSpace(command.IdentificationType) ||
            string.IsNullOrWhiteSpace(command.IdentificationNumber) || string.IsNullOrWhiteSpace(command.Name))
            throw new LendingRuleException("La identificación y el nombre del cliente son obligatorios.");
        if (command.IdentificationType.Trim().Length > 30 ||
            command.IdentificationNumber.Trim().Length > 60 || command.Name.Trim().Length > 250 ||
            command.Address?.Trim().Length > 400 || command.Phone?.Trim().Length > 60 ||
            command.WhatsApp?.Trim().Length > 60 || command.Email?.Trim().Length > 250 ||
            command.City?.Trim().Length > 120 ||
            command.EconomicActivity?.Trim().Length > 200 ||
            command.Employer?.Trim().Length > 250)
            throw new LendingRuleException("Un dato del cliente supera la longitud permitida.");
        if (command.MonthlyIncome < 0)
            throw new LendingRuleException("Los ingresos no pueden ser negativos.");
        var client = new Client
        {
            CompanyId = companyId,
            IdentificationType = command.IdentificationType.Trim(),
            IdentificationNumber = command.IdentificationNumber.Trim(),
            Name = command.Name.Trim(),
            Address = command.Address?.Trim(),
            Phone = command.Phone?.Trim(),
            WhatsApp = command.WhatsApp?.Trim(),
            Email = command.Email?.Trim(),
            City = command.City?.Trim(),
            BirthDate = command.BirthDate,
            EconomicActivity = command.EconomicActivity?.Trim(),
            MonthlyIncome = command.MonthlyIncome,
            Employer = command.Employer?.Trim()
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return client;
    }

    public Task<Client?> GetClientAsync(Guid companyId, Guid clientId, CancellationToken ct) =>
        db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == clientId, ct);

    public async Task<ClientReference> CreateClientReferenceAsync(Guid companyId, Guid clientId,
        CreateClientReference command, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(x => x.CompanyId == companyId && x.Id == clientId, ct))
            throw new LendingRuleException("El cliente no existe en la empresa.");
        if (!Enum.IsDefined(command.Kind) || string.IsNullOrWhiteSpace(command.Name) ||
            command.Name.Trim().Length > 250 || command.Relationship?.Trim().Length > 120 ||
            command.Phone?.Trim().Length > 60 || command.Email?.Trim().Length > 250 ||
            command.Notes?.Trim().Length > 1000)
            throw new LendingRuleException("La referencia del cliente no es válida.");
        var reference = new ClientReference
        {
            CompanyId = companyId, ClientId = clientId, Kind = command.Kind,
            Name = command.Name.Trim(), Relationship = command.Relationship?.Trim(),
            Phone = command.Phone?.Trim(), Email = command.Email?.Trim(),
            Notes = command.Notes?.Trim()
        };
        db.ClientReferences.Add(reference);
        await db.SaveChangesAsync(ct);
        return reference;
    }

    public async Task<CreditProduct> CreateProductAsync(Guid companyId, CreateProduct command, CancellationToken ct)
    {
        await RequireCompany(companyId, ct);
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            throw new LendingRuleException("El código y nombre del producto son obligatorios.");
        if (command.Code.Trim().Length > 50 || command.Name.Trim().Length > 200 ||
            command.Description?.Trim().Length > 1000)
            throw new LendingRuleException("Un dato del producto supera la longitud permitida.");
        var product = new CreditProduct
        {
            CompanyId = companyId, Code = command.Code.Trim().ToUpperInvariant(),
            Name = command.Name.Trim(), Description = command.Description?.Trim()
        };
        db.CreditProducts.Add(product);
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<ProductVersion> CreateProductVersionAsync(Guid companyId, Guid productId,
        CreateProductVersion command, CancellationToken ct)
    {
        var product = await db.CreditProducts.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == productId, ct)
            ?? throw new LendingRuleException("El producto no existe en la empresa.");
        if (!product.IsActive) throw new LendingRuleException("El producto está inactivo.");
        var company = await db.Companies.AsNoTracking().SingleAsync(x => x.Id == companyId, ct);
        string currency = (command.CurrencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3 || currency != company.CurrencyCode)
            throw new LendingRuleException("La moneda del producto debe coincidir con la moneda operativa de la empresa.");
        if (command.MinimumAmount <= 0 || command.MaximumAmount < command.MinimumAmount ||
            command.MinimumInstallments < 1 || command.MaximumInstallments < command.MinimumInstallments ||
            command.Rate < 0 || command.CurrencyDecimals is < 0 or > 4 ||
            command.InsurancePerInstallment < 0 || command.OtherChargePerInstallment < 0 ||
            command.LateAnnualRate < 0 || command.LateGraceDays < 0 ||
            command.LateMaximumPerInstallment < 0 || !Enum.IsDefined(command.LateGraceMode) ||
            !Enum.IsDefined(command.RateKind) || !Enum.IsDefined(command.DayCount) ||
            !Enum.IsDefined(command.Frequency) || !Enum.IsDefined(command.Method) ||
            command.ValidFrom == default ||
            Math.Round(command.MinimumAmount, command.CurrencyDecimals) != command.MinimumAmount ||
            Math.Round(command.MaximumAmount, command.CurrencyDecimals) != command.MaximumAmount ||
            Math.Round(command.InsurancePerInstallment, command.CurrencyDecimals) != command.InsurancePerInstallment ||
            Math.Round(command.OtherChargePerInstallment, command.CurrencyDecimals) != command.OtherChargePerInstallment)
            throw new LendingRuleException("Las condiciones del producto no son válidas.");
        if (command.AllowsCapitalization && command.Method != AmortizationMethod.Bullet)
            throw new LendingRuleException("La capitalización inicial requiere pago único.");
        var priority = command.PaymentPriority ?? PaymentEngine.DefaultPolicy.Priority;
        if (priority.Count != Enum.GetValues<PaymentCategory>().Length ||
            priority.Distinct().Count() != priority.Count ||
            Enum.GetValues<PaymentCategory>().Except(priority).Any())
            throw new LendingRuleException("La prioridad de pago debe ordenar cada componente una vez.");
        int nextNumber = (await db.ProductVersions.Where(x => x.CompanyId == companyId && x.ProductId == productId)
            .MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
        var version = new ProductVersion
        {
            CompanyId = companyId, ProductId = productId, Number = nextNumber,
            IsPublished = true, ValidFrom = command.ValidFrom,
            CurrencyCode = currency, MinimumAmount = command.MinimumAmount,
            MaximumAmount = command.MaximumAmount, MinimumInstallments = command.MinimumInstallments,
            MaximumInstallments = command.MaximumInstallments, Rate = command.Rate,
            RateKind = command.RateKind, DayCount = command.DayCount,
            Frequency = command.Frequency, Method = command.Method,
            CurrencyDecimals = command.CurrencyDecimals,
            AllowsCapitalization = command.AllowsCapitalization,
            InsurancePerInstallment = command.InsurancePerInstallment,
            OtherChargePerInstallment = command.OtherChargePerInstallment,
            LateAnnualRate = command.LateAnnualRate,
            LateGraceDays = command.LateGraceDays,
            LateGraceMode = command.LateGraceMode,
            LateMaximumPerInstallment = command.LateMaximumPerInstallment,
            PaymentPriority = string.Join(',', priority)
        };
        db.ProductVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<Loan> CreateLoanAsync(Guid companyId, CreateLoan command, CancellationToken ct)
    {
        await RequireCompany(companyId, ct);
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == command.ClientId, ct)
            ?? throw new LendingRuleException("El cliente no existe en la empresa.");
        if (client.Status != ClientStatus.Active) throw new LendingRuleException("El cliente está inactivo.");
        var version = await db.ProductVersions.AsNoTracking().SingleOrDefaultAsync(
            x => x.CompanyId == companyId && x.Id == command.ProductVersionId, ct)
            ?? throw new LendingRuleException("La versión del producto no existe en la empresa.");
        var product = await db.CreditProducts.AsNoTracking().SingleAsync(
            x => x.CompanyId == companyId && x.Id == version.ProductId, ct);
        if (!product.IsActive || !version.IsPublished || version.ValidFrom > command.RequestDate)
            throw new LendingRuleException("La versión del producto no está disponible.");
        if (command.Principal < version.MinimumAmount || command.Principal > version.MaximumAmount ||
            command.Installments < version.MinimumInstallments || command.Installments > version.MaximumInstallments ||
            Math.Round(command.Principal, version.CurrencyDecimals) != command.Principal)
            throw new LendingRuleException("Monto o plazo fuera de los límites del producto.");
        if (command.CapitalizeInterest && !version.AllowsCapitalization)
            throw new LendingRuleException("El producto no permite capitalización.");
        if (string.IsNullOrWhiteSpace(command.Number) || command.Number.Trim().Length > 50 ||
            command.RequestDate == default)
            throw new LendingRuleException("El número del préstamo es obligatorio.");
        var loan = new Loan
        {
            CompanyId = companyId, ClientId = client.Id, ProductId = product.Id,
            ProductVersionId = version.Id, Number = command.Number.Trim(),
            RequestDate = command.RequestDate, OriginalPrincipal = command.Principal,
            RequestedInstallments = command.Installments,
            RequestedCapitalization = command.CapitalizeInterest,
            CurrencyCode = version.CurrencyCode
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync(ct);
        return loan;
    }

    public async Task<Loan> RequestLoanAsync(Guid companyId, Guid loanId, CancellationToken ct)
    {
        var loan = await RequireLoan(companyId, loanId, ct);
        if (loan.Status != LoanStatus.Draft) throw new LendingRuleException("Solo un borrador puede solicitarse.");
        loan.Status = LoanStatus.Requested;
        await db.SaveChangesAsync(ct);
        return loan;
    }

    public async Task<Loan> ApproveLoanAsync(Guid companyId, Guid loanId, ApproveLoan command, CancellationToken ct)
    {
        var loan = await RequireLoan(companyId, loanId, ct);
        if (loan.Status != LoanStatus.Requested) throw new LendingRuleException("Solo un préstamo solicitado puede aprobarse.");
        if (command.ApprovalDate < loan.RequestDate)
            throw new LendingRuleException("La aprobación no puede anteceder a la solicitud.");
        var productVersion = await db.ProductVersions.AsNoTracking().SingleAsync(
            x => x.CompanyId == companyId && x.Id == loan.ProductVersionId, ct);
        loan.Terms = new LoanTerms
        {
            CompanyId = companyId, LoanId = loan.Id,
            SourceProductVersionId = productVersion.Id,
            Rate = productVersion.Rate, RateKind = productVersion.RateKind,
            DayCount = productVersion.DayCount, Frequency = productVersion.Frequency,
            Method = productVersion.Method, Installments = loan.RequestedInstallments,
            CurrencyDecimals = productVersion.CurrencyDecimals,
            CapitalizeInterest = loan.RequestedCapitalization,
            InsurancePerInstallment = productVersion.InsurancePerInstallment,
            OtherChargePerInstallment = productVersion.OtherChargePerInstallment,
            LateAnnualRate = productVersion.LateAnnualRate,
            LateGraceDays = productVersion.LateGraceDays,
            LateGraceMode = productVersion.LateGraceMode,
            LateMaximumPerInstallment = productVersion.LateMaximumPerInstallment,
            PaymentPriority = productVersion.PaymentPriority
        };
        loan.ApprovalDate = command.ApprovalDate;
        loan.Status = LoanStatus.Approved;
        await db.SaveChangesAsync(ct);
        return loan;
    }

    public async Task<Loan> RejectLoanAsync(Guid companyId, Guid loanId,
        RejectLoan command, Guid actorUserId, CancellationToken ct)
    {
        var loan = await RequireLoan(companyId, loanId, ct);
        if (loan.Status != LoanStatus.Requested)
            throw new LendingRuleException("Solo un préstamo solicitado puede rechazarse.");
        string reason = command.Reason?.Trim() ?? string.Empty;
        if (command.DecisionDate < loan.RequestDate || reason.Length is < 3 or > 1000 ||
            actorUserId == Guid.Empty)
            throw new LendingRuleException("El rechazo requiere fecha, motivo y responsable válidos.");
        loan.Status = LoanStatus.Rejected;
        db.LoanRejections.Add(new LoanRejection
        {
            CompanyId = companyId, LoanId = loanId, DecisionDate = command.DecisionDate,
            Reason = reason, ActorUserId = actorUserId
        });
        await db.SaveChangesAsync(ct);
        return loan;
    }

    public async Task<Loan> DisburseLoanAsync(Guid companyId, Guid loanId, DisburseLoan command,
        Guid actorUserId, CancellationToken ct)
    {
        var loan = await db.Loans.Include(x => x.Terms)
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == loanId, ct)
            ?? throw new LendingRuleException("El préstamo no existe en la empresa.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new LendingRuleException("El desembolso requiere una clave de idempotencia.");
        if (command.IdempotencyKey.Trim().Length > 120)
            throw new LendingRuleException("La clave de desembolso supera la longitud permitida.");
        if (actorUserId == Guid.Empty)
            throw new LendingRuleException("El desembolso requiere un usuario identificado.");
        var application = await db.CreditApplications.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId, ct);
        if (loan.Status == LoanStatus.Active)
        {
            bool sameEvent = await db.LoanEvents.AnyAsync(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.Type == LoanEventType.Disbursement && x.IdempotencyKey == command.IdempotencyKey, ct);
            if (sameEvent)
            {
                if (loan.DisbursementDate != command.DisbursementDate ||
                    loan.FirstDueDate != command.FirstDueDate)
                    throw new LendingRuleException("La clave de desembolso ya se usó con otras fechas.");
                return loan;
            }
        }
        if (loan.Status != LoanStatus.Approved || loan.Terms is null)
            throw new LendingRuleException("Solo un préstamo aprobado puede desembolsarse.");
        if (application is not null &&
            (application.Status != CreditApplicationStatus.Approved ||
             !await db.CreditApplicationDocuments.AnyAsync(x =>
                 x.CompanyId == companyId && x.ApplicationId == application.Id &&
                 x.Kind == "SignedContract", ct)))
            throw new LendingRuleException("Adjunta el contrato firmado antes de desembolsar esta solicitud.");
        if (command.DisbursementDate < loan.ApprovalDate || command.FirstDueDate <= command.DisbursementDate)
            throw new LendingRuleException("Las fechas de desembolso y primera cuota no son válidas.");

        var terms = loan.Terms;
        SimulationResult simulation = simulator.Simulate(new SimulationRequest(
            loan.OriginalPrincipal, terms.Rate, terms.RateKind, terms.DayCount,
            terms.Frequency, terms.Installments, command.DisbursementDate,
            command.FirstDueDate, terms.Method, terms.CurrencyDecimals,
            terms.CapitalizeInterest, null, terms.InsurancePerInstallment,
            terms.OtherChargePerInstallment));

        var plan = new LoanPlanVersion
        {
            CompanyId = companyId, LoanId = loanId, Number = 1,
            IsCurrent = true, Reason = "Desembolso inicial"
        };
        db.LoanPlans.Add(plan);
        foreach (var line in simulation.Schedule)
            db.LoanInstallments.Add(new LoanInstallment
            {
                CompanyId = companyId, LoanId = loanId, PlanId = plan.Id,
                Number = line.Number, DueDate = line.DueDate,
                OpeningPrincipal = line.OpeningPrincipal, Principal = line.Principal,
                Interest = line.Interest, CapitalizedInterest = line.CapitalizedInterest,
                Insurance = line.Insurance, OtherCharges = line.OtherCharges,
                Payment = line.Payment, ClosingPrincipal = line.ClosingPrincipal
            });
        var evt = new LoanEvent
        {
            CompanyId = companyId, LoanId = loanId, Type = LoanEventType.Disbursement,
            EffectiveDate = command.DisbursementDate, OriginReference = loan.Number,
            IdempotencyKey = command.IdempotencyKey.Trim(), ActorUserId = actorUserId
        };
        db.LoanEvents.Add(evt);
        db.OutboxMessages.Add(new OutboxMessage
        {
            CompanyId = companyId, EventType = "LoanDisbursed", AggregateId = evt.Id,
            PayloadJson = JsonSerializer.Serialize(new
            {
                companyId, loanId, eventId = evt.Id, loanNumber = loan.Number,
                command.DisbursementDate, amount = loan.OriginalPrincipal,
                loan.CurrencyCode
            })
        });
        db.LoanMovements.Add(new LoanMovement
        {
            CompanyId = companyId, LoanId = loanId, EventId = evt.Id,
            Sequence = 1, Component = BalanceComponent.Principal,
            Amount = loan.OriginalPrincipal, CurrencyCode = loan.CurrencyCode
        });
        loan.DisbursementDate = command.DisbursementDate;
        loan.FirstDueDate = command.FirstDueDate;
        loan.Status = LoanStatus.Active;
        if (application is not null) application.Status = CreditApplicationStatus.Disbursed;
        await db.SaveChangesAsync(ct);
        return loan;
    }

    private async Task RequireCompany(Guid companyId, CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, ct))
            throw new LendingRuleException("La empresa no existe o está inactiva.");
    }

    private async Task<Loan> RequireLoan(Guid companyId, Guid loanId, CancellationToken ct) =>
        await db.Loans.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == loanId, ct)
        ?? throw new LendingRuleException("El préstamo no existe en la empresa.");
}
