using Abg.Domain.Algorithms;
using Abg.Domain.Client;
using Abg.Domain.Contracts;
using Abg.Domain.PolicyForms;
using Abg.Domain.Service;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Abg.Domain.Constants;

namespace BlazorApp1.Components.Shared;

public partial class ServiceCheckout : IDisposable
{
    [Parameter] public ClientRequest         Request            { get; set; } = new();
    [Parameter] public ServiceCollectionResp Services           { get; set; } = new();
    [Parameter] public EventCallback<string> OnRemove           { get; set; }
    [Parameter] public EventCallback         OnClose            { get; set; }
    [Parameter] public EventCallback         OnCompleted        { get; set; }
    [Parameter] public EventCallback         OnSchedulesChanged { get; set; }

    // CLAUDE.md §8: 3-minute payment window; poll our own status endpoint sparingly.
    private const int QrTimeoutSeconds     = 180;
    private const int StatusPollIntervalSeconds = 12;

    bool showForm;
    bool showSignIn;
    bool showSuccess;
    bool showQr;
    bool showNailsRules;
    bool showConsentForm;
    bool isLoading;

    bool nailsRulesAccepted;
    bool consentAccepted;
    bool emailLockedFromAccount;

    string? qrImageUrl;
    string  completedBookingId = "";
    CancellationTokenSource? timerCts;

    string? consumerError;
    bool showConfirmModal;

    int qrCountdownSeconds = QrTimeoutSeconds;
    string qrCountdownDisplay => TimeSpan.FromSeconds(qrCountdownSeconds).ToString(@"mm\:ss");

    bool ShowSummary => !showForm && !showSignIn && !showSuccess && !showQr && !showNailsRules && !showConsentForm;

    string GoogleLoginHref   => $"{Auth.GoogleLoginUrl}?post_login_redirect_uri=/";
    string FacebookLoginHref => $"{Auth.FacebookLoginUrl}?post_login_redirect_uri=/";

    private async Task Remove(string uid)
        => await OnRemove.InvokeAsync(uid);

    private async Task OpenForm()
    {
        var user = await Auth.GetUserAsync();

        if (user is null)
        {
            showSignIn = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            Request.ClientInformation.Email = user.Email;
            emailLockedFromAccount = true;
        }

        showForm = true;
    }

    private void CloseSignIn()
        => showSignIn = false;

    private void CloseForm()
    {
        consumerError = null;
        showForm      = false;
    }

    private void OpenConfirmModal()
    {
        consumerError    = string.Empty;
        showConfirmModal = true;
    }

    private void CloseConfirmModal()
        => showConfirmModal = false;

    private async Task ConfirmProceed()
    {
        showConfirmModal = false;
        await Submit();
    }

    private decimal GetTotalAmount()
        => CheckoutSummaryAlgorithms.GetTotalAmount(Request);

    private string GetBranchDisplayName(ServiceBranch branch)
        => CheckoutSummaryAlgorithms.GetBranchDisplayName(branch);

    private async Task Submit()
    {
        consumerError = null;

        Request.ClientConsent ??= new ConsentModel();

        showForm = false;

        var nextStep = CheckoutPolicyAlgorithms.ResolveNextStep(
            Request,
            nailsRulesAccepted,
            consentAccepted);

        if (nextStep == CheckoutFlowStep.NailsRules)
        {
            showNailsRules  = true;
            showConsentForm = false;
            return;
        }

        if (nextStep == CheckoutFlowStep.ConsentForm)
        {
            showNailsRules  = false;
            showConsentForm = true;
            return;
        }

        await StartPaymentAsync();
    }

    private async Task HandleNailsRulesAccepted()
    {
        nailsRulesAccepted = true;
        showNailsRules     = false;

        var nextStep = CheckoutPolicyAlgorithms.ResolveNextStep(
            Request,
            nailsRulesAccepted,
            consentAccepted);

        if (nextStep == CheckoutFlowStep.ConsentForm)
        {
            Request.ClientConsent ??= new ConsentModel();

            showConsentForm = true;
            return;
        }

        await StartPaymentAsync();
    }

    private void BackFromNailsRules()
    {
        showNailsRules = false;
        showForm       = true;
    }

    private async Task HandleConsentAccepted()
    {
        consentAccepted = true;
        showConsentForm = false;
        await StartPaymentAsync();
    }

    private void BackFromConsentForm()
    {
        showConsentForm = false;

        if (CheckoutPolicyAlgorithms.RequiresNailsRules(Request) && nailsRulesAccepted)
        {
            showNailsRules = true;
            return;
        }

        showForm = true;
    }

    private async Task StartPaymentAsync()
    {
        isLoading     = true;
        consumerError = null;
        StateHasChanged();

        try
        {
            var booking = await Api.CreateBookingAsync(Request);

            Request.ClientInformation.ClientBookingId = booking.BookingId;

            var payment = await Api.CreateQrphPaymentAsync(booking.BookingId);

            qrImageUrl = payment.QrImageUrl;

            showQr          = true;
            showForm        = false;
            showNailsRules  = false;
            showConsentForm = false;

            qrCountdownSeconds = payment.ExpiresInSeconds > 0 ? payment.ExpiresInSeconds : QrTimeoutSeconds;

            timerCts?.Cancel();
            timerCts?.Dispose();
            timerCts = new CancellationTokenSource();

            _ = RunPaymentWatchAsync(booking.BookingId, timerCts.Token);
        }
        catch (Exception ex)
        {
            showQr          = false;
            showForm        = true;
            showNailsRules  = false;
            showConsentForm = false;
            qrImageUrl      = null;
            consumerError   = ex.Message;
        }
        finally
        {
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// One-second countdown for the UI; every 12th tick checks the booking's
    /// payment status server-side (the Paymongo webhook flips it to paid).
    /// </summary>
    private async Task RunPaymentWatchAsync(string bookingId, CancellationToken ct)
    {
        try
        {
            var elapsed = 0;

            while (!ct.IsCancellationRequested && qrCountdownSeconds > 0)
            {
                await Task.Delay(1000, ct);
                qrCountdownSeconds--;
                elapsed++;

                if (elapsed % StatusPollIntervalSeconds == 0)
                {
                    var resolved = await CheckPaymentStatusAsync(bookingId);

                    if (resolved)
                        return;
                }

                await InvokeAsync(StateHasChanged);
            }

            if (!ct.IsCancellationRequested && qrCountdownSeconds <= 0)
            {
                // Final check: the payment may have landed in the last poll gap.
                if (!await CheckPaymentStatusAsync(bookingId))
                    await InvokeAsync(HandlePaymentExpired);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task<bool> CheckPaymentStatusAsync(string bookingId)
    {
        PaymentStatusResponse status;

        try
        {
            status = await Api.GetPaymentStatusAsync(bookingId);
        }
        catch (ApiException)
        {
            // Transient poll failure (rate limit, network) — keep counting down.
            return false;
        }

        if (status.Status == PaymentStatusResponse.Paid)
        {
            await InvokeAsync(async () =>
            {
                timerCts?.Cancel();
                await OnSchedulesChanged.InvokeAsync();
                await ShowSuccessStateAsync(bookingId);
                StateHasChanged();
            });

            return true;
        }

        if (status.Status == PaymentStatusResponse.Expired)
        {
            await InvokeAsync(() =>
            {
                timerCts?.Cancel();
                ShowPaymentErrorState("The booking hold expired before payment was confirmed. Please book again.");
                StateHasChanged();
            });

            return true;
        }

        return false;
    }

    private void HandlePaymentExpired()
    {
        timerCts?.Cancel();

        showQr             = false;
        qrImageUrl         = null;
        qrCountdownSeconds = QrTimeoutSeconds;
        showForm           = true;

        consumerError = "The QR code expired. Please click Proceed again to generate a new QR code.";

        StateHasChanged();
    }

    private async Task CancelPayment()
    {
        timerCts?.Cancel();

        showQr             = false;
        qrImageUrl         = null;
        qrCountdownSeconds = QrTimeoutSeconds;
        showForm           = true;

        await Task.CompletedTask;
    }

    private async Task Close()
    {
        timerCts?.Cancel();
        await OnClose.InvokeAsync();
    }

    private async Task Finish()
    {
        timerCts?.Cancel();
        ResetCheckoutState();
        await OnCompleted.InvokeAsync();
    }

    private async Task ShowSuccessStateAsync(string bookingId)
    {
        showForm           = false;
        showQr             = false;
        showNailsRules     = false;
        showConsentForm    = false;
        qrImageUrl         = null;
        qrCountdownSeconds = QrTimeoutSeconds;
        completedBookingId = bookingId;
        showSuccess        = true;

        StateHasChanged();

        // The proof-of-purchase QR encodes only the booking id; the admin
        // scanner resolves it to the booking record (CLAUDE.md §9).
        await Task.Delay(50);
        await JS.InvokeAsync<bool>("renderBookingQr", "proof-qr", bookingId);
    }

    private void ShowPaymentErrorState(string message)
    {
        showSuccess        = false;
        showQr             = false;
        showNailsRules     = false;
        showConsentForm    = false;
        qrImageUrl         = null;
        qrCountdownSeconds = QrTimeoutSeconds;
        showForm           = true;
        consumerError      = message;
    }

    private void ResetCheckoutState()
    {
        showSuccess           = false;
        showForm              = false;
        showSignIn            = false;
        showQr                = false;
        showNailsRules        = false;
        showConsentForm       = false;
        qrImageUrl            = null;
        qrCountdownSeconds    = QrTimeoutSeconds;
        consumerError         = null;
        nailsRulesAccepted    = false;
        consentAccepted       = false;
        completedBookingId    = "";
        Request.ClientConsent = new ConsentModel();
    }

    public void Dispose()
    {
        timerCts?.Cancel();
        timerCts?.Dispose();
    }
}
