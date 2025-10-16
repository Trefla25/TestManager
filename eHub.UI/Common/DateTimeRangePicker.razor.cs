using eHub.UI.Localization;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace eHub.UI.Common;

public partial class DateTimeRangePicker : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }

    [Parameter] public DateTime Start { get; set; }
    [Parameter] public DateTime End { get; set; }
    [Parameter] public EventCallback<DateTime> StartChanged { get; set; }
    [Parameter] public EventCallback<DateTime> EndChanged { get; set; }

    private void OnStartDateChanged(DateTime? date) => UpdateStart(date + Start.TimeOfDay);
    private void OnStartTimeChanged(TimeSpan? time) => UpdateStart(Start.Date + time);
    private void OnEndDateChanged(DateTime? date) => UpdateEnd(date + End.TimeOfDay);
    private void OnEndTimeChanged(TimeSpan? time) => UpdateEnd(End.Date + time);

    private void UpdateStart(DateTime? start)
    {
        if (start is not null && start != Start)
        {
            Start = start.Value;
            StartChanged.InvokeAsync(start.Value);
        }
    }

    private void UpdateEnd(DateTime? end)
    {
        if (end is not null && end != End)
        {
            End = end.Value;
            EndChanged.InvokeAsync(end.Value);
        }
    }
}
