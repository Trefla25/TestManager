using eHub.Contracts;
using eHub.UI.Util;
using MudBlazor;

namespace eHub.UI.Models;
public class PacketColumnFilter
{
    public string? ColumnName { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public bool OperatorEnabled => ColumnName != null;
    public bool ValueEnabled => ColumnName != null && Operator != null && (Operator is not FilterOperator.Number.Empty and not FilterOperator.Number.NotEmpty);

    public bool IsValidFilter()
    {
        if (ColumnName is null || Operator is null || ((Operator != FilterOperator.Number.Empty && Operator != FilterOperator.Number.NotEmpty) && Value is null))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public ColumnFilterDto ToColumnFilterDto()
    {
        if (ColumnName is null)
        {
            throw new Exception("Column Name is null");
        }
        if (Operator is null)
        {
            throw new Exception("Operator is null");
        }

        var columnFilterOperator = FilterOperatorUtil.GetColumnFilterOperator(Operator);

        return new ColumnFilterDto(ColumnName, columnFilterOperator, Value);
    }

    public override string ToString() => $"{ColumnName} {Operator} {Value}";
}
