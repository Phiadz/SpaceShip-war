namespace Battleship2D.Presentation.ViewModels;
/// <summary>
/* 
tự code tay một class ObservableObject riêng bằng các thư viện tiêu chuẩn có sẵn của .NET (System.ComponentModel để gọi giao diện INotifyPropertyChanged ). 
Đây là một cách làm rất phổ biến và tối ưu khi không muốn project bị phụ thuộc vào các thư viện bên ngoài.
Vì class ObservableObject của bạn dùng hàm SetProperty để thông báo cho giao diện WPF biết khi nào dữ liệu thay đổi, 
nên file VisualPlacedShipViewModel.cs mới sẽ cần được viết theo đúng cấu trúc (pattern) đó thay vì dùng auto-property (như { get; set; }).
*/ 
public class VisualPlacedShipViewModel : ObservableObject
{
    private string _imagePath = string.Empty;
    private double _left;
    private double _top;
    private double _width;
    private double _height;
    private double _rotation;

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

    public double Rotation
    {
        get => _rotation;
        set => SetProperty(ref _rotation, value);
    }
}