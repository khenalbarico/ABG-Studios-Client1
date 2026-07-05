using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp1.Services;

public sealed class AuthUser
{
    public string IdentityProvider { get; set; } = "";
    public string UserId           { get; set; } = "";
    public string UserDetails      { get; set; } = "";
    public List<string> UserRoles  { get; set; } = [];

    public string Email => UserDetails.Contains('@') ? UserDetails : "";
}

/// <summary>
/// Reads the Static Web Apps auth session from /.auth/me and exposes
/// the provider login/logout endpoints. One request per session.
/// </summary>
public sealed class AuthService(HttpClient _http)
{
    sealed class AuthPayload
    {
        [JsonPropertyName("clientPrincipal")]
        public AuthUser? ClientPrincipal { get; set; }
    }

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    AuthUser? _user;
    bool _loaded;

    public AuthUser? User => _user;
    public bool IsAuthenticated => _user is not null;

    public string GoogleLoginUrl   => "/.auth/login/google";
    public string FacebookLoginUrl => "/.auth/login/facebook";
    public string LogoutUrl        => "/.auth/logout";

    public async Task<AuthUser?> GetUserAsync(CancellationToken ct = default)
    {
        if (_loaded)
            return _user;

        try
        {
            var payload = await _http.GetFromJsonAsync<AuthPayload>("/.auth/me", JsonOptions, ct);
            _user = payload?.ClientPrincipal;
        }
        catch (HttpRequestException)
        {
            // Local dev without the SWA emulator has no /.auth endpoint.
            _user = null;
        }
        catch (JsonException)
        {
            _user = null;
        }

        _loaded = true;

        return _user;
    }
}
