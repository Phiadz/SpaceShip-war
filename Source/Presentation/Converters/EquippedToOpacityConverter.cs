using System;
using System.Globalization;
using System.Windows.Data;

namespace Battleship2D.Presentation.Converters;

public class EquippedToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Nếu truyền vào là true (đã gắn lên tàu) -> Trả về 0.4 (Mờ đi 60%)
        if (value is bool isEquipped && isEquipped)
        {
            return 0.4;
        }
        
        // Nếu false (chưa gắn) -> Trả về 1.0 (Rõ nét 100%)
        return 1.0; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException(); // Không cần dùng chiều ngược lại
    }
}