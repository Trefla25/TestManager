using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace eHub.UI.Util;

public static class XmlUtil
{
    public static bool TryParse(string? xml, [NotNullWhen(true)] out XDocument? xmlDocument)
    {
        if (string.IsNullOrEmpty(xml))
        {
            xmlDocument = new XDocument();
            return false;
        }

        try
        {
            xmlDocument = XDocument.Parse(xml);
            return true;
        }
        catch (XmlException)
        {
            xmlDocument = null;
            return false;
        }
    }

    public static bool TryGetElementFromPath(this XDocument xdoc, string? path, out XElement? xmlElement)
    {
        if (string.IsNullOrEmpty(path))
        {
            xmlElement = null;
            return false;
        }

        xmlElement = xdoc.XPathSelectElement(path);

        return xmlElement != null;
    }
}
