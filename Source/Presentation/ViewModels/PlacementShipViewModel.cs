namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// Dai dien mot tau can dat trong placement phase.
/// </summary>
public sealed class PlacementShipViewModel : ObservableObject
{
    private bool _isPlaced;

    public PlacementShipViewModel(string name, int length)
    {
        Name = name;
        Length = length;
    }

    public string Name { get; }
    public int Length { get; }

    public bool IsPlaced
    {
        get => _isPlaced;
        set
        {
            if (SetProperty(ref _isPlaced, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName => IsPlaced ? $"{Name} ({Length}) - DONE" : $"{Name} ({Length})";
}
