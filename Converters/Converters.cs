using System.Globalization;

namespace HEBRaffle.Converters;

// ════════════════════════════════════════════════════════════════════════════
// BoolToColorConverter
// Usage: IsWinner → Green accent / Transparent
// ════════════════════════════════════════════════════════════════════════════

public sealed class BoolToColorConverter : IValueConverter
{
    public Color TrueColor  { get; set; } = Colors.Green;
    public Color FalseColor { get; set; } = Colors.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ════════════════════════════════════════════════════════════════════════════
// InvertBoolConverter
// ════════════════════════════════════════════════════════════════════════════

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

// ════════════════════════════════════════════════════════════════════════════
// BoolToVisibilityConverter  (alias — maps bool → bool, Identity)
// ════════════════════════════════════════════════════════════════════════════

public sealed class NullToFalseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ════════════════════════════════════════════════════════════════════════════
// IntToColorConverter — for badge backgrounds
// ════════════════════════════════════════════════════════════════════════════

public sealed class IntToColorConverter : IValueConverter
{
    public Color PositiveColor { get; set; } = Color.FromArgb("#CC0000");
    public Color ZeroColor     { get; set; } = Color.FromArgb("#9E9E9E");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n > 0 ? PositiveColor : ZeroColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ════════════════════════════════════════════════════════════════════════════
// StringNullOrEmptyToFalseConverter
// ════════════════════════════════════════════════════════════════════════════

public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
