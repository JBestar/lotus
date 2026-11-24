using System.Globalization;
using System.Windows.Data;
using LotusOddEven.Core.Models;

namespace LotusOddEven.App.Converters;

public sealed class AlgorithmKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BettingAlgorithmKind kind)
        {
            return kind switch
            {
                BettingAlgorithmKind.Martingale => "마틴",
                BettingAlgorithmKind.Cruise => "크루즈",
                _ => kind.ToString()
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
