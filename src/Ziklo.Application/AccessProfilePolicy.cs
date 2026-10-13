using Ziklo.Domain;

namespace Ziklo.Application;

public static class AccessProfilePolicy
{
    public static long CreateMask(IEnumerable<BusinessPermission> permissions)
    {
        long mask = 0;
        foreach (var permission in permissions)
        {
            if (!Enum.IsDefined(permission) || permission == BusinessPermission.ManagePermissions)
                throw new ArgumentException("El perfil contiene un permiso no asignable.", nameof(permissions));
            mask |= 1L << (int)permission;
        }
        return mask;
    }

    public static bool IsAllowed(long mask, BusinessPermission permission) =>
        Enum.IsDefined(permission) && permission != BusinessPermission.ManagePermissions &&
        (mask & (1L << (int)permission)) != 0;
}
