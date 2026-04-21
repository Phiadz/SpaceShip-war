namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// ViewModel cho mot hinh tau de hien thi trong panel Fleet Assets.
/// </summary>
public sealed class FleetAssetViewModel
{
    public FleetAssetViewModel(string name, string imagePath)
    {
        Name = name;
        ImagePath = imagePath;
    }

    public string Name { get; }
    public string ImagePath { get; }
}
