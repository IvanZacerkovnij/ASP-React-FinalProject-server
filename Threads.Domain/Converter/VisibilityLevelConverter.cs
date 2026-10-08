using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using Threads.Domain.Enums;

namespace Threads.Domain.Converter;

public sealed class VisibilityLevelConverter : EnumConverter
{
    public VisibilityLevelConverter()
        : base(typeof(VisibilityLevel))
    {
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
        {
            return base.ConvertFrom(context, culture, value);
        }

        foreach (var field in typeof(VisibilityLevel).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name == text)
            {
                return field.GetValue(null);
            }
        }

        throw new FormatException($"'{text}' is not a valid visibility level.");
    }
}
