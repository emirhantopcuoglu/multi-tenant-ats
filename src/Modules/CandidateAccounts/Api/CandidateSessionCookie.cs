using Microsoft.AspNetCore.Http;

namespace Ats.Modules.CandidateAccounts.Api;

// The candidate refresh token travels only in this cookie, with the same protections as the
// company one: HttpOnly, SameSite=Strict and a path limited to the candidate auth endpoints.
internal static class CandidateSessionCookie
{
    private const string Name = "ats_candidate_refresh";
    private const string Path = "/api/v1/candidate/auth";

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
