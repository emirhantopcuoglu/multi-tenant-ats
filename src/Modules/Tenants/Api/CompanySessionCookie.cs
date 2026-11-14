using Microsoft.AspNetCore.Http;

namespace Ats.Modules.Tenants.Api;

// The company refresh token travels only in this cookie. HttpOnly keeps it from page scripts,
// SameSite=Strict from requests other sites start, and the path from every other API call. The
// path follows the route, so it changes with the API version.
internal static class CompanySessionCookie
{
    private const string Name = "ats_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string refreshToken, DateTime expiresAtUtc) =>
        response.Cookies.Append(Name, refreshToken, Options(expiresAtUtc));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expiresAtUtc: null));

    private static CookieOptions Options(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAtUtc,
    };
}
