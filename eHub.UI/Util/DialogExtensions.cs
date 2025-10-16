using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace eHub.UI.Util;

internal static class DialogExtensions
{
    public static DialogBuilder<T> Create<T>(this IDialogService dialogService) where T : ComponentBase
        => new(dialogService);
}

internal class DialogBuilder<TComponent> where TComponent : ComponentBase
{
    private readonly IDialogService _dialogService;

    private string? _title;
    private readonly DialogParameters _parameters = [];
    private DialogOptions? _options;

    public DialogBuilder(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public DialogBuilder<TComponent> Title(string title)
    {
        _title = title;
        return this;
    }

    // Using two generics to prevent the compiler from trying to find the lowest common type
    // - TProp when used the intended way will infer to the exact property type
    // - TValue then can be any type that is assignable to TProp
    public DialogBuilder<TComponent> Param<TProp, TValue>(Expression<Func<TComponent, TProp>> property, TValue value) where TValue : TProp
    {
        if (property.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Cannot read property from expression", nameof(property));
        }

        var parameterName = memberExpression.Member.Name;
        _parameters[parameterName] = value;
        return this;
    }

    public DialogBuilder<TComponent> Options(DialogOptions options)
    {
        _options = options;
        return this;
    }

    public IDialogReference Show() => _dialogService.Show<TComponent>(_title, _parameters, _options);
    public Task<IDialogReference> ShowAsync() => _dialogService.ShowAsync<TComponent>(_title, _parameters, _options);
}
