namespace Wealthra.Application.Common.Security;

/// <summary>Authorization policy names (ASP.NET Core <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute.Policy"/>).</summary>
public static class AuthPolicies
{
    /// <summary>SuperAdmin or legacy Admin — full destructive platform control.</summary>
    public const string AdminElevated = "AdminElevated";

    /// <summary>Finance + elevated — subscription plans, pricing, FX, revenue analytics.</summary>
    public const string FinanceTeam = "FinanceTeam";

    /// <summary>Support + elevated — tickets, announcements, user usage visibility.</summary>
    public const string SupportTeam = "SupportTeam";

    /// <summary>Any internal staff role.</summary>
    public const string AnyStaff = "AnyStaff";

    /// <summary>Legacy name; same as <see cref="AdminElevated"/>.</summary>
    public const string AdminOnly = "AdminOnly";
}
