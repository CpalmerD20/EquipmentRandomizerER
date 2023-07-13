﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace ERBingoRandomizer.Converter; 

internal class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("There is no backwards conversion for count to bool");
    }
}
