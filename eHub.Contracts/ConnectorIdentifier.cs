using System.ComponentModel;
using System.Globalization;

namespace eHub.Contracts;

[TypeConverter(typeof(ConnectorIdentifierConverter))]
public readonly record struct ConnectorIdentifier(string Instance, string ConnectorKey)
{
    public override string ToString() => $"{Instance}:{ConnectorKey}";


    private class ConnectorIdentifierConverter : TypeConverter
    {
        private static readonly char[] SplitOn = [':'];

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str && str.Split(SplitOn, 2) is [var instance, var key])
            {
                return new ConnectorIdentifier(instance, key);
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
