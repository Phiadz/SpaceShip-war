//File này lưu tọa độ (X, Y) của ụ súng tính từ góc trên cùng bên trái của con tàu (không phải của cả bản đồ).
namespace Battleship2D.Presentation.ViewModels;

public class EquippedWeaponViewModel : ObservableObject
{
    private string _imagePath = string.Empty;
    private double _left;
    private double _top;
    private double _width;
    private double _height;

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public double Left
    {
        get => _left;
        set => SetProperty(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => SetProperty(ref _top, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }
}