using System.Collections.Frozen;
using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.Localization;
using eHub.UI.Models;
using eHub.UI.Util;
using eController.WebUI.Utility.DynamicComponent;
using eMessenger;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace eHub.UI.Common;

public partial class PacketFilterDialog : FrameworkComponent
{
    [Inject] public required IMessenger Messenger { get; init; }
    [Inject] public required NavigationManager NavigationManager { get; init; }
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required ILogger<PacketFilterDialog> Logger { get; init; }
    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
    [Parameter] public DateTime StartDate { get; set; }
    [Parameter] public DateTime EndDate { get; set; }
    [Parameter] public List<PacketColumnFilter> CurrentColumnFilterOptions { get; set; } = [];
    [Parameter] public IReadOnlyDictionary<string, Type> CustomFilters { get; init; } = FrozenDictionary<string, Type>.Empty;

    private readonly List<PacketColumnFilter> _newPacketColumnFilters = [];

    private ImmutableArray<string> _packetColumns = [];


    protected override void OnParametersSet()
    {
        foreach (var filterOption in CurrentColumnFilterOptions)
        {
            var filterOptionCopy = eController.Util.ObjUtil.Clone(filterOption);
            _newPacketColumnFilters.Add(filterOptionCopy);
        }

        if (_newPacketColumnFilters.Count == 0)
        {
            _newPacketColumnFilters.Add(new PacketColumnFilter());
        }

        _packetColumns = [.. PacketDto.PacketColumns, .. CustomFilters.Keys];
    }

    private void Submit()
    {
        _newPacketColumnFilters.RemoveAll(x => !x.IsValidFilter());
        MudDialog.Close(DialogResult.Ok(new PacketFilter(StartDate, EndDate, _newPacketColumnFilters)));
    }

    private void Cancel()
    {
        _newPacketColumnFilters.RemoveAll(x => !x.IsValidFilter());
        MudDialog.Cancel();
    }

    private void AddNewFilter()
    {
        _newPacketColumnFilters.Add(new PacketColumnFilter());
    }

    private void RemoveAllColumnFilters()
    {
        _newPacketColumnFilters.Clear();
    }

    private void RemoveFilter(PacketColumnFilter filter)
    {
        _newPacketColumnFilters.Remove(filter);
    }

    private void FilteredColumnChanged(PacketColumnFilter filter, string? columnName)
    {
        filter.ColumnName = columnName;
        filter.Operator = GetOperatorsByColumnName(columnName).FirstOrDefault();
    }

    private string[] GetOperatorsByColumnName(string? columnName)
    {
        if (columnName is null)
        {
            return [];
        }

        return FilterOperatorUtil.GetOperators(GetColumnFieldType(columnName));
    }

    private FieldType GetColumnFieldType(string columnName)
    {
        if (!CustomFilters.TryGetValue(columnName, out var columnType))
        {
            columnType = typeof(PacketDto).GetProperty(columnName)?.PropertyType;
        }

        var fieldType = FieldType.Identify(columnType);

        return fieldType;
    }

    private static void FilteredDateChanged(PacketColumnFilter filter, DateTime? date)
    {
        TimeSpan time;

        if (DateTime.TryParse(filter.Value, out var oldDate))
        {
            time = oldDate.TimeOfDay;
        }
        else
        {
            time = TimeSpan.Zero;
        }

        filter.Value = (date + time).ToString();
    }

    private static void FilteredTimeChanged(PacketColumnFilter filter, TimeSpan? time)
    {
        DateTime date;

        if (DateTime.TryParse(filter.Value, out var oldDate))
        {
            date = oldDate.Date;
        }
        else
        {
            date = DateTime.Now.Date;
        }

        filter.Value = (date + time).ToString();
    }

    private static DateTime? GetDateFromValue(string? value)
    {
        if (DateTime.TryParse(value, out var date))
        {
            return date.Date;
        }
        return null;
    }

    private static TimeSpan? GetTimeFromValue(string? value)
    {
        if (DateTime.TryParse(value, out var date))
        {
            return date.TimeOfDay;
        }
        return null;
    }

    public record PacketFilter(DateTime Start, DateTime End, List<PacketColumnFilter> PacketColumFilters);
}
