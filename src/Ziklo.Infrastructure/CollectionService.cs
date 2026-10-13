using System.Data;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class CollectionService(ZikloDbContext db) : ICollectionService
{
    public async Task<CollectionActivityView> CreateAsync(Guid companyId, Guid loanId,
        CreateCollectionActivity command, Guid actorUserId, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == companyId && x.IsActive, ct)
            ?? throw new LendingRuleException("La empresa no existe o está inactiva.");
        DateOnly today = DailyCloseService.BusinessDate(company);
        string result = command.Result?.Trim() ?? string.Empty;
        string notes = command.Notes?.Trim() ?? string.Empty;
        if (actorUserId == Guid.Empty || !Enum.IsDefined(command.Channel) ||
            command.ContactDate == default || command.ContactDate > today ||
            command.NextContactDate < command.ContactDate ||
            result.Length is < 1 or > 120 || notes.Length > 2000 ||
            command.PromiseDueDate.HasValue != command.PromisedAmount.HasValue)
            throw new LendingRuleException("La gestión de cobranza contiene datos inválidos.");
        if (command.PromiseDueDate.HasValue &&
            (command.PromiseDueDate <= command.ContactDate ||
             command.PromiseDueDate < today || command.PromisedAmount <= 0))
            throw new LendingRuleException("La promesa de pago contiene datos inválidos.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var loan = await db.Loans.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == loanId, ct)
            ?? throw new LendingRuleException("El préstamo no existe en la empresa.");
        if (loan.Status is not (LoanStatus.Active or LoanStatus.InArrears) ||
            command.ContactDate < loan.DisbursementDate)
            throw new LendingRuleException("El préstamo no está habilitado para cobranza.");
        if (command.PromisedAmount.HasValue)
        {
            int decimals = await db.LoanTerms.AsNoTracking().Where(x =>
                x.CompanyId == companyId && x.LoanId == loanId)
                .Select(x => x.CurrencyDecimals).SingleAsync(ct);
            if (Math.Round(command.PromisedAmount.Value, decimals) != command.PromisedAmount.Value)
                throw new LendingRuleException("La promesa supera los decimales de la moneda.");
        }
        if (command.PromiseDueDate.HasValue && await db.PaymentPromises.AnyAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId && x.Status == PromiseStatus.Pending, ct))
            throw new LendingRuleException("El préstamo ya tiene una promesa pendiente.");
        if (command.PromiseDueDate.HasValue && await db.DailyCloseRuns.AnyAsync(x =>
            x.CompanyId == companyId && x.BusinessDate >= command.PromiseDueDate, ct))
            throw new LendingRuleException("La fecha de la promesa ya está cerrada.");
        var activity = new CollectionActivity
        {
            CompanyId = companyId, LoanId = loanId, ClientId = loan.ClientId,
            ActorUserId = actorUserId, ContactDate = command.ContactDate,
            Channel = command.Channel, Result = result, Notes = notes,
            NextContactDate = command.NextContactDate
        };
        db.CollectionActivities.Add(activity);
        PaymentPromise? promise = null;
        if (command.PromiseDueDate.HasValue)
        {
            promise = new PaymentPromise
            {
                CompanyId = companyId, LoanId = loanId,
                CollectionActivityId = activity.Id,
                StartDate = command.ContactDate,
                DueDate = command.PromiseDueDate.Value,
                PromisedAmount = command.PromisedAmount!.Value
            };
            db.PaymentPromises.Add(promise);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToView(activity, promise);
    }

    public async Task<IReadOnlyList<CollectionActivityView>> ListAsync(Guid companyId,
        Guid loanId, CancellationToken ct)
    {
        var activities = await db.CollectionActivities.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.LoanId == loanId)
            .OrderByDescending(x => x.ContactDate).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var promises = await db.PaymentPromises.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.LoanId == loanId).ToListAsync(ct);
        var byActivity = promises.ToDictionary(x => x.CollectionActivityId);
        return activities.Select(x => ToView(x, byActivity.GetValueOrDefault(x.Id))).ToList();
    }

    private static CollectionActivityView ToView(CollectionActivity activity,
        PaymentPromise? promise) => new(activity.Id, activity.LoanId, activity.ClientId,
        activity.ContactDate, activity.Channel, activity.Result, activity.Notes,
        activity.NextContactDate, promise?.Id, promise?.DueDate,
        promise?.PromisedAmount, promise?.Status);
}
