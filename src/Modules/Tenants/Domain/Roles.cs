namespace Ats.Modules.Tenants.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Recruiter = "Recruiter";
    public const string HiringManager = "HiringManager";
    public const string ReadOnly = "ReadOnly";

    public static readonly string[] All = { Admin, Recruiter, HiringManager, ReadOnly };
}
