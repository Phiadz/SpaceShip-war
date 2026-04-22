using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Battleship2D.Networking.Protocol;
using Battleship2D.Presentation.Commands;

namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// Dai dien mot o tren ban do 10x10 de binding len UI.
/// </summary>
public sealed class BoardCellViewModel : ObservableObject
{
    private static readonly Brush WaterBrush = new SolidColorBrush(Color.FromArgb(170, 21, 42, 66));
    private static readonly Brush TransparentBrush = Brushes.Transparent;
    private static readonly Brush ShipBrush = new SolidColorBrush(Color.FromRgb(70, 120, 185));
    private static readonly Brush MissBrush = new SolidColorBrush(Color.FromRgb(78, 96, 110));
    private static readonly Brush HitBrush = new SolidColorBrush(Color.FromRgb(192, 64, 64));
    private static readonly Brush SunkBrush = new SolidColorBrush(Color.FromRgb(122, 26, 26));

    private readonly bool _isEnemyBoard;
    private Brush _cellBrush;
    private string? _localShipImagePath;
    private string _marker = string.Empty;
    private bool _isClickable;

    public BoardCellViewModel(int x, int y, bool isEnemyBoard, Func<BoardCellViewModel, Task>? onFireAsync = null)
    {
        X = x;
        Y = y;
        IsEnemyBoard = isEnemyBoard;
        _isEnemyBoard = isEnemyBoard;
        _cellBrush = isEnemyBoard ? TransparentBrush : WaterBrush;

        FireCommand = new AsyncRelayCommand(
            async () =>
            {
                if (!IsClickable)
                {
                    return;
                }

                if (onFireAsync is not null)
                {
                    await onFireAsync(this);
                }
            },
            () => true);
    }

    public int X { get; }
    public int Y { get; }
    public bool IsEnemyBoard { get; }

    public AsyncRelayCommand CellCommand => FireCommand;
    public AsyncRelayCommand FireCommand { get; }

    public Brush CellBrush
    {
        get => _cellBrush;
        private set => SetProperty(ref _cellBrush, value);
    }

    public string Marker
    {
        get => _marker;
        private set => SetProperty(ref _marker, value);
    }

    public string? LocalShipImagePath
    {
        get => _localShipImagePath;
        private set => SetProperty(ref _localShipImagePath, value);
    }

    public bool IsClickable
    {
        get => _isClickable;
        private set
        {
            if (SetProperty(ref _isClickable, value))
            {
                FireCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Dat trang thai local ship de hien thi doi hinh ban dau.
    /// </summary>
    public void SetLocalShipPresent(bool isPresent, string? imagePath = null)
    {
        if (isPresent)
        {
            CellBrush = ShipBrush;
            LocalShipImagePath = imagePath;
        }
        else if (_isEnemyBoard)
        {
            CellBrush = TransparentBrush;
            LocalShipImagePath = null;
        }
        else
        {
            CellBrush = WaterBrush;
            LocalShipImagePath = null;
        }

        if (!isPresent)
        {
            Marker = string.Empty;
        }
    }

    /// <summary>
    /// Cap nhat ket qua khi board local bi ban.
    /// </summary>
    public void ApplyIncomingOutcome(ShotOutcome outcome)
    {
        LocalShipImagePath = null;

        switch (outcome)
        {
            case ShotOutcome.Miss:
                CellBrush = MissBrush;
                Marker = "o";
                break;

            case ShotOutcome.Hit:
                CellBrush = HitBrush;
                Marker = "X";
                break;

            case ShotOutcome.Sunk:
                CellBrush = SunkBrush;
                Marker = "#";
                break;
        }
    }

    /// <summary>
    /// Cap nhat ket qua khi local player ban vao board doi thu.
    /// </summary>
    public void ApplyOutgoingOutcome(ShotOutcome outcome)
    {
        ApplyIncomingOutcome(outcome);
        IsClickable = false;
    }

    /// <summary>
    /// Cho phep hoac khoa thao tac click tren o nay.
    /// </summary>
    public void SetClickable(bool clickable)
    {
        IsClickable = clickable;
    }

    /// <summary>
    /// Tuong thich nguoc voi code cu cho enemy board.
    /// </summary>
    public void SetEnemyClickable(bool clickable)
    {
        SetClickable(clickable);
    }
}
