namespace BlazorApp1.Services;

/// <summary>
/// Resolves the Function App base URL for the current environment.
///
/// Local dev: wwwroot/appsettings.Development.json sets Api:BaseAddress to the
/// local Functions host (http://localhost:7071/).
///
/// Deployed (DEV and PROD SWA): Api:BaseAddress is intentionally absent, so the
/// UI calls its own origin and Static Web Apps proxies /api/* to the Function
/// App linked to that SWA. The proxy is also what forwards the signed-in
/// user's x-ms-client-principal header, so deployed builds must not point at a
/// Function App URL directly.
/// </summary>
public static class ApiBaseUrl
{
    public static string Resolve(string? configured, string hostBaseAddress)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return hostBaseAddress;
        }

        return configured.EndsWith('/') ? configured : configured + "/";
    }
}
