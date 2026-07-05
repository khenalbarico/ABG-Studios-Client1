using Abg.Domain.Algorithms;
using Abg.Domain.Client;
using Abg.Domain.Schedules;
using Abg.Domain.__Base__;
using Microsoft.AspNetCore.Components;
using static Abg.Domain.Constants;

namespace BlazorApp1.Components.Shared;

public partial class ServiceSection
{
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public List<BaseSvcStructure> Services { get; set; } = [];
    [Parameter] public List<string> BookedUids { get; set; } = [];
    [Parameter] public List<ApptSchedRec> AppointmentSchedules { get; set; } = [];
    [Parameter] public List<ClientService> CurrentBookings { get; set; } = [];
    [Parameter] public EventCallback<ClientService> OnBook { get; set; }
    [Parameter] public ScheduleCfg ScheduleCfg { get; set; } = new();

    readonly Dictionary<string, DateTime> CurrentMonth = [];
    readonly Dictionary<string, DateTime> SelectedDates = [];
    readonly Dictionary<string, string> timeSelections = [];
    readonly Dictionary<string, ServiceDesigns> designSelections = [];
    readonly Dictionary<string, ServiceBranch?> branchSelections = [];
    readonly Dictionary<string, bool> imageLoadedStates = [];
    readonly Dictionary<string, bool> imageErrorStates = [];

    bool showImagePreview;
    string previewImageUrl = "";
    string previewTitle = "";

    protected override void OnParametersSet()
    {
        foreach (var svc in Services)
        {
            var cardKey = GetCardKey(svc);

            if (!CurrentMonth.ContainsKey(cardKey))
                CurrentMonth[cardKey] = DateTime.Today;

            if (!branchSelections.ContainsKey(cardKey))
                branchSelections[cardKey] = null;

            if (!timeSelections.ContainsKey(cardKey))
                timeSelections[cardKey] = "";

            if (IsNailsService() && !designSelections.ContainsKey(cardKey))
                designSelections[cardKey] = ServiceDesigns.Simple;
        }
    }

    private string GetCardKey(BaseSvcStructure svc)
        => ServiceSectionKeyAlgorithms.BuildCardKey(Title, svc);

    private bool IsNailsService()
        => string.Equals(Title, "Nails", StringComparison.OrdinalIgnoreCase);

    private string GetMonthLabel(string cardKey)
        => CurrentMonth[cardKey].ToString("MMMM yyyy");

    private void ChangeMonth(string cardKey, int offset)
        => CurrentMonth[cardKey] = CurrentMonth[cardKey].AddMonths(offset);

    private string GetSelectedBranchValue(string cardKey)
    {
        if (branchSelections.TryGetValue(cardKey, out var value) && value.HasValue)
            return value.Value.ToString();

        return "";
    }

    private bool HasSelectedBranch(string cardKey)
        => branchSelections.TryGetValue(cardKey, out var value) && value.HasValue;

    private void OnBranchChanged(string cardKey, string? value)
    {
        ServiceBranch? parsedBranch = null;

        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<ServiceBranch>(value, out var branch))
            parsedBranch = branch;

        var existingBranch = branchSelections.TryGetValue(cardKey, out var current) ? current : null;
        var changed = existingBranch != parsedBranch;

        branchSelections[cardKey] = parsedBranch;

        if (changed)
        {
            SelectedDates.Remove(cardKey);
            timeSelections[cardKey] = "";
            CurrentMonth[cardKey] = DateTime.Today;
        }
    }

    private string GetBranchLabel(ServiceBranch branch)
        => branch.ToString();

    private string GetSelectedDesignValue(string cardKey)
    {
        if (designSelections.TryGetValue(cardKey, out var value))
            return value.ToString();

        return ServiceDesigns.Simple.ToString();
    }

    private ServiceDesigns GetSelectedDesign(string cardKey)
    {
        if (designSelections.TryGetValue(cardKey, out var value))
            return value;

        return ServiceDesigns.Simple;
    }

    private void OnDesignChanged(string cardKey, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<ServiceDesigns>(value, out var design))
            designSelections[cardKey] = design;
        else
            designSelections[cardKey] = ServiceDesigns.Simple;
    }

    private List<string> GetDesignImages(ServiceDesigns design)
    {
        var designName = design.ToString();
        var folder = designName.ToLowerInvariant();

        return
        [
            $"/imgs/designs/{folder}/{designName}1.jpg",
            $"/imgs/designs/{folder}/{designName}2.jpg",
            $"/imgs/designs/{folder}/{designName}3.jpg"
        ];
    }

    private List<CalendarDay> GetCalendarDays(BaseSvcStructure svc, string cardKey)
    {
        return ServiceSectionCalendarAlgorithms.BuildCalendarDays(
            CurrentMonth[cardKey],
            svc.ScheduleSlots.DaySlots,
            date => HasAnyAvailableSlotForDate(svc, cardKey, date));
    }

    private async Task SelectDate(string cardKey, DateTime date)
    {
        SelectedDates[cardKey]  = date.Date;
        timeSelections[cardKey] = "";

        await InvokeAsync(StateHasChanged);
    }

    private bool IsSelectedDate(string cardKey, DateTime date)
        => SelectedDates.ContainsKey(cardKey) && SelectedDates[cardKey].Date == date.Date;

    private bool HasSelectedDate(string cardKey)
        => SelectedDates.ContainsKey(cardKey);

    private string GetSelectedTime(string cardKey)
    {
        if (timeSelections.TryGetValue(cardKey, out var value))
            return value;

        return "";
    }

    private void OnTimeChanged(string cardKey, string? value)
    {
        timeSelections[cardKey] = value ?? "";
    }

    private bool HasAnyAvailableSlotForDate(BaseSvcStructure svc, string cardKey, DateTime date)
    {
        var timeSlots = svc.ScheduleSlots.TimeSlots ?? [];

        if (timeSlots.Count == 0)
            return false;

        return timeSlots.Any(slot => GetTimeSlotStatusForDate(svc, cardKey, date, slot) == TimeSlotStatus.Available);
    }

    private bool HasAvailableSelectedTime(BaseSvcStructure svc, string cardKey)
    {
        if (!HasSelectedDate(cardKey))
            return false;

        var selectedTime = GetSelectedTime(cardKey);

        if (string.IsNullOrWhiteSpace(selectedTime))
            return false;

        return GetTimeSlotStatus(svc, cardKey, selectedTime) == TimeSlotStatus.Available;
    }

    private TimeSlotStatus GetTimeSlotStatus(BaseSvcStructure svc, string cardKey, string timeSlot)
    {
        if (!SelectedDates.ContainsKey(cardKey))
            return TimeSlotStatus.Available;

        return GetTimeSlotStatusForDate(svc, cardKey, SelectedDates[cardKey], timeSlot);
    }

    private TimeSlotStatus GetTimeSlotStatusForDate(BaseSvcStructure svc, string cardKey, DateTime date, string timeSlot)
    {
        if (IsAlreadyBookedInCurrentSelection(cardKey, date, timeSlot))
            return TimeSlotStatus.BookedByYou;

        if (IsTimeSlotFull(svc, cardKey, date, timeSlot))
            return TimeSlotStatus.Full;

        return TimeSlotStatus.Available;
    }

    private string GetTimeSlotLabel(BaseSvcStructure svc, string cardKey, string timeSlot)
    {
        var status = GetTimeSlotStatus(svc, cardKey, timeSlot);

        if (status == TimeSlotStatus.BookedByYou)
        {
            var conflict = GetCurrentBookingConflict(cardKey, SelectedDates[cardKey], timeSlot);

            if (conflict is not null)
            {
                var serviceLabel = !string.IsNullOrWhiteSpace(conflict.ServiceDetails)
                    ? conflict.ServiceDetails
                    : conflict.ServiceName;

                return $"{timeSlot} - Already booked for {serviceLabel}";
            }

            return $"{timeSlot} - Already booked on another service";
        }

        if (status == TimeSlotStatus.Full)
            return ServiceSectionCapacityAlgorithms.GetFullSlotLabel(
                svc,
                Title,
                SelectedDates[cardKey],
                timeSlot,
                ScheduleCfg);

        return timeSlot;
    }

    private string GetFullSlotMessage(BaseSvcStructure svc, string cardKey, string timeSlot)
    {
        if (!SelectedDates.TryGetValue(cardKey, out var selectedDate))
            return "This time slot is not available.";

        return ServiceSectionCapacityAlgorithms.GetFullSlotMessage(
            svc,
            Title,
            selectedDate,
            timeSlot,
            ScheduleCfg);
    }

    private ClientService? GetCurrentBookingConflict(string cardKey, DateTime date, string timeSlot)
    {
        var slotDateTime = ServiceSectionTimeAlgorithms.CombineDateAndTime(date, timeSlot);

        return CurrentBookings.FirstOrDefault(x =>
            !string.Equals(x.ServiceUid, cardKey, StringComparison.OrdinalIgnoreCase) &&
            x.ServiceDate == slotDateTime);
    }

    private bool IsAlreadyBookedInCurrentSelection(string cardKey, DateTime date, string timeSlot)
        => GetCurrentBookingConflict(cardKey, date, timeSlot) is not null;

    private bool IsTimeSlotFull(BaseSvcStructure svc, string cardKey, DateTime date, string timeSlot)
    {
        return ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            svc,
            Title,
            date,
            timeSlot,
            AppointmentSchedules,
            CurrentBookings,
            ScheduleCfg,
            cardKey);
    }

    private bool IsImageLoaded(string imageUrl, string designName)
    {
        var key = GetImageStateKey(imageUrl, designName);
        return imageLoadedStates.TryGetValue(key, out var loaded) && loaded;
    }

    private bool HasImageError(string imageUrl, string designName)
    {
        var key = GetImageStateKey(imageUrl, designName);
        return imageErrorStates.TryGetValue(key, out var hasError) && hasError;
    }

    private string GetImageStateKey(string imageUrl, string designName)
        => $"{designName}::{imageUrl}";

    private void MarkImageLoaded(string imageUrl, string designName)
    {
        var key = GetImageStateKey(imageUrl, designName);
        imageLoadedStates[key] = true;
        imageErrorStates[key] = false;
    }

    private void MarkImageError(string imageUrl, string designName)
    {
        var key = GetImageStateKey(imageUrl, designName);
        imageLoadedStates[key] = false;
        imageErrorStates[key] = true;
    }

    private void OpenImagePreview(string imageUrl, string title)
    {
        previewImageUrl = imageUrl;
        previewTitle    = title;

        var key = GetImageStateKey(imageUrl, title);
        imageLoadedStates[key] = false;
        imageErrorStates[key]  = false;

        showImagePreview = true;
    }

    private void CloseImagePreview()
    {
        showImagePreview = false;
        previewImageUrl  = "";
        previewTitle     = "";
    }

    private async Task Book(BaseSvcStructure svc, string cardKey)
    {
        if (!SelectedDates.ContainsKey(cardKey))
            return;

        var selectedTime = GetSelectedTime(cardKey);

        if (string.IsNullOrWhiteSpace(selectedTime))
            return;

        var slotStatus = GetTimeSlotStatus(svc, cardKey, selectedTime);

        if (slotStatus != TimeSlotStatus.Available)
            return;

        if (!branchSelections.TryGetValue(cardKey, out var selectedBranch) || !selectedBranch.HasValue)
            return;

        var combinedDate = ServiceSectionTimeAlgorithms.CombineDateAndTime(SelectedDates[cardKey], selectedTime);

        var data = new ClientService
        {
            ServiceUid     = cardKey,
            ServiceName    = Title,
            ServiceDesign  = IsNailsService() ? GetSelectedDesign(cardKey).ToString() : "",
            ServiceDetails = svc.Details,
            ServiceCost    = svc.Cost,
            Branch         = selectedBranch.Value,
            ServiceDate    = combinedDate
        };

        await OnBook.InvokeAsync(data);
    }
}
