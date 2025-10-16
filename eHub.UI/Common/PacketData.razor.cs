using eHub.PlugIn.UI;
using eHub.UI.Util;
using System.Text.Json;
using System.Xml.Linq;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using eHub.UI.Localization;
using eHub.Contracts.UIConfig;

namespace eHub.UI.Common;

public partial class PacketData : FrameworkComponent
{
	[Inject] public required IStringLocalizer<Language> Localizer { get; init; }

	[Parameter] public required PacketDto Packet { get; set; }
	[Parameter] public required DataDisplayConfig DataDisplay { get; set; }
	[Parameter] public EventCallback OnOpenDataDialog { get; set; }

    private string? _data;
	private DisplayFormat _dataDisplayFormat;
	private HashSet<TreeItemData>? _treeDataDisplay;

	private class TreeItemData
	{
		public string Title { get; set; } = default!;
		public bool IsRoot { get; set; }
		public HashSet<TreeItemData> TreeItems { get; set; } = default!;
	}

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		if (_data == Packet.Data) { return; }

		_data = Packet.Data;
		_dataDisplayFormat = DataDisplay.Format ?? DisplayFormat.SingleLine;

		if (_dataDisplayFormat.IsXml())
		{
			ParseXmlData();
		}
		else if (_dataDisplayFormat.IsJson())
		{
			ParseJsonData();
		}

		return;
	}

    private void ParseXmlData()
    {
        if (!XmlUtil.TryParse(Packet.Data, out var xmlDocument) || xmlDocument?.Root == null)
        {
            _treeDataDisplay = null;
            _dataDisplayFormat = DisplayFormat.SingleLine;
            return;
        }

        var xmlRoot = ParseXml(xmlDocument.Root);
        xmlRoot.IsRoot = true;
        _treeDataDisplay = [xmlRoot];
    }

    private void ParseJsonData()
    {
        if (!JsonUtil.TryParse(Packet.Data, out var jsonDocument) || jsonDocument == null)
        {
            _treeDataDisplay = null;
            _dataDisplayFormat = DisplayFormat.SingleLine;
            return;
        }

        var jsonRoot = new TreeItemData()
        {
            Title = "JSON",
            IsRoot = true,
            TreeItems = ParseJson(jsonDocument.RootElement)
        };
        _treeDataDisplay = [jsonRoot];
    }

	private TreeItemData ParseXml(XElement element)
	{
		var node = new TreeItemData
		{
			Title = element.Name.LocalName,
			TreeItems = []
		};

		if (!element.HasElements)
		{
			node.TreeItems.Add(new TreeItemData() { Title = element.Value });
			return node;
		}

		foreach (var childElement in element.Elements())
		{
			node.TreeItems.Add(ParseXml(childElement));
		}

		return node;
	}

	private HashSet<TreeItemData> ParseJson(JsonElement element)
	{
		var nodes = new HashSet<TreeItemData>();

		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var childElement in element.EnumerateObject())
			{
				var node = new TreeItemData()
				{
					Title = childElement.Name,
					TreeItems = ParseJson(childElement.Value)
				};

				nodes.Add(node);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var childElement in element.EnumerateArray())
			{
				nodes = ParseJson(childElement);
			}
		}
		else
		{
			nodes.Add(new TreeItemData() { Title = element.ToString() });
		}

		return nodes;
	}
}
