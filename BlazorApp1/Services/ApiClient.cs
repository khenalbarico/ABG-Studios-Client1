using Abg.Domain.Client;
using Abg.Domain.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlazorApp1.Services;

/// <summary>Raised when the API answers with a user-facing error (validation, capacity, auth).</summary>
public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ApiClient(HttpClient _http)
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogResponse> GetCatalogAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var url = $"api/catalog?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        return await ReadAsync<CatalogResponse>(await _http.GetAsync(url, ct), ct);
    }

    public async Task<CreateBookingResponse> CreateBookingAsync(ClientRequest request, CancellationToken ct = default)
        => await ReadAsync<CreateBookingResponse>(
            await _http.PostAsJsonAsync("api/bookings", request, JsonOptions, ct), ct);

    public async Task<CreateQrphPaymentResponse> CreateQrphPaymentAsync(string bookingId, CancellationToken ct = default)
        => await ReadAsync<CreateQrphPaymentResponse>(
            await _http.PostAsJsonAsync("api/payments/qrph", new CreateQrphPaymentRequest { BookingId = bookingId }, JsonOptions, ct), ct);

    public async Task<PaymentStatusResponse> GetPaymentStatusAsync(string bookingId, CancellationToken ct = default)
        => await ReadAsync<PaymentStatusResponse>(
            await _http.GetAsync($"api/payments/status?bookingId={Uri.EscapeDataString(bookingId)}", ct), ct);

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);

            return payload ?? throw new ApiException("The server response could not be read.", response.StatusCode);
        }

        string message;

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, ct);
            message   = string.IsNullOrWhiteSpace(error?.Message)
                ? DefaultMessageFor(response.StatusCode)
                : error.Message;
        }
        catch (JsonException)
        {
            message = DefaultMessageFor(response.StatusCode);
        }

        throw new ApiException(message, response.StatusCode);
    }

    static string DefaultMessageFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized    => "Please sign in to continue.",
        HttpStatusCode.TooManyRequests => "Too many requests. Please wait a moment and try again.",
        _                              => "Something went wrong. Please try again."
    };
}
