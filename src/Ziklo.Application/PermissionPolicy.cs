using Ziklo.Domain;

namespace Ziklo.Application;

public static class PermissionPolicy
{
    public static bool IsAllowed(InitialRole role, BusinessPermission permission,
        IReadOnlyDictionary<BusinessPermission, bool>? overrides = null)
    {
        if (!Enum.IsDefined(role) || !Enum.IsDefined(permission)) return false;
        if (permission == BusinessPermission.ManagePermissions)
            return role == InitialRole.Administrator;
        if ((role is InitialRole.CreditAnalyst or InitialRole.Originator) &&
            (permission is BusinessPermission.ApproveLoan or BusinessPermission.DisburseLoan))
            return false;
        if (role == InitialRole.Originator && permission == BusinessPermission.StudyApplications)
            return false;
        if (role == InitialRole.CreditApprover &&
            (permission is BusinessPermission.CreateLoan or BusinessPermission.DisburseLoan or
                BusinessPermission.StudyApplications))
            return false;
        if (overrides is not null && overrides.TryGetValue(permission, out bool allowed))
            return allowed;
        return role switch
        {
            InitialRole.Administrator => true,
            InitialRole.Manager => permission != BusinessPermission.ManagePermissions,
            InitialRole.CreditAnalyst => permission is
                BusinessPermission.ViewClients or BusinessPermission.ManageClients or
                BusinessPermission.ViewLoans or BusinessPermission.CreateLoan or
                BusinessPermission.StudyApplications or
                BusinessPermission.ViewReports,
            InitialRole.Originator => permission is
                BusinessPermission.ViewClients or BusinessPermission.ManageClients or
                BusinessPermission.ViewLoans or BusinessPermission.CreateLoan,
            InitialRole.CreditApprover => permission is
                BusinessPermission.ViewClients or BusinessPermission.ViewLoans or
                BusinessPermission.ApproveLoan or BusinessPermission.ViewReports,
            InitialRole.Cashier => permission is
                BusinessPermission.ViewClients or BusinessPermission.ViewLoans or
                BusinessPermission.DisburseLoan or BusinessPermission.RegisterPayment,
            InitialRole.Collector => permission is
                BusinessPermission.ViewClients or BusinessPermission.ViewLoans or
                BusinessPermission.RecordCollection or BusinessPermission.ViewReports,
            InitialRole.Auditor => permission is
                BusinessPermission.ViewClients or BusinessPermission.ViewLoans or
                BusinessPermission.ViewReports or BusinessPermission.ViewAudit,
            InitialRole.ReadOnly => permission is
                BusinessPermission.ViewClients or BusinessPermission.ViewLoans or
                BusinessPermission.ViewReports,
            _ => false
        };
    }
}
