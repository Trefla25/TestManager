using Microsoft.Extensions.Localization;
using MudBlazor;

namespace eHub.UI.Localization;

public class CustomMudLocalizer(IStringLocalizer<CustomMudLocalizer> localizer) : MudLocalizer
{
    public override LocalizedString this[string key] => localizer[key];
}
