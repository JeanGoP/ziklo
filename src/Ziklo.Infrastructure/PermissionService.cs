using System.Data;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class PermissionService(ZikloDbContext db) : IPermissionService
{
    public async Task<IReadOnlyList<PermissionView>> ListAsync(Guid companyId,
        CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, ct))
            throw new LendingRuleException("La empresa no existe o está inactiva.");
        var overrides = await db.CompanyRolePermissions.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var byKey = overrides.ToDictionary(x => (x.Role, x.Permission), x => x.Allowed);
        return (from role in Enum.GetValues<InitialRole>()
                from permission in Enum.GetValues<BusinessPermission>()
                select new PermissionView(role, permission,
                    PermissionPolicy.IsAllowed(role, permission,
                        byKey.TryGetValue((role, permission), out bool allowed)
                            ? new Dictionary<BusinessPermission, bool> { [permission] = allowed }
                            : null),
                    byKey.ContainsKey((role, permission)))).ToList();
    }

    public async Task<PermissionView> SetAsync(Guid companyId, InitialRole role,
        BusinessPermission permission, bool allowed, Guid actorUserId,
        string ipAddress, CancellationToken ct)
    {
        if (!Enum.IsDefined(role) || !Enum.IsDefined(permission) || actorUserId == Guid.Empty ||
            ipAddress.Length > 60)
            throw new LendingRuleException("La modificación de permiso no es válida.");
        if (role == InitialRole.Administrator && permission == BusinessPermission.ManagePermissions)
            throw new LendingRuleException("El permiso de administración principal es permanente.");
        if (allowed && ((permission == BusinessPermission.ManagePermissions &&
                         role != InitialRole.Administrator) ||
                        ((role is InitialRole.CreditAnalyst or InitialRole.Originator) &&
                         (permission is BusinessPermission.ApproveLoan or BusinessPermission.DisburseLoan)) ||
                        (role == InitialRole.Originator && permission == BusinessPermission.StudyApplications) ||
                        (role == InitialRole.CreditApprover &&
                         (permission is BusinessPermission.CreateLoan or BusinessPermission.DisburseLoan or
                             BusinessPermission.StudyApplications))))
            throw new LendingRuleException("Este permiso está restringido por separación de funciones.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, ct))
            throw new LendingRuleException("La empresa no existe o está inactiva.");
        var grant = await db.CompanyRolePermissions.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Role == role && x.Permission == permission, ct);
        bool previous = grant?.Allowed ?? PermissionPolicy.IsAllowed(role, permission);
        if (grant is null)
        {
            grant = new CompanyRolePermission
            {
                CompanyId = companyId, Role = role, Permission = permission,
                Allowed = allowed, ChangedByUserId = actorUserId
            };
            db.CompanyRolePermissions.Add(grant);
        }
        else
        {
            grant.Allowed = allowed;
            grant.ChangedByUserId = actorUserId;
            grant.ChangedAtUtc = DateTime.UtcNow;
        }
        if (previous != allowed)
            db.PermissionChanges.Add(new PermissionChange
            {
                CompanyId = companyId, Role = role, Permission = permission,
                PreviousAllowed = previous, NewAllowed = allowed,
                ActorUserId = actorUserId, IpAddress = ipAddress
            });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PermissionView(role, permission, allowed, true);
    }
}
