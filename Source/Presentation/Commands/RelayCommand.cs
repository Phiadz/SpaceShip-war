using System;
using System.Windows.Input;

namespace Battleship2D.Presentation.Commands;

/// <summary>
/// Command dong bo don gian cho thao tac UI nhanh.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Khoi tao command dong bo.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Kiem tra command co duoc phep chay o thoi diem hien tai.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>
    /// Thuc thi action dong bo.
    /// </summary>
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// Yeu cau WPF reevaluate trang thai enabled/disabled cua button.
    /// </summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
