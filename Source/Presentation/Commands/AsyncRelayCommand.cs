using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Battleship2D.Presentation.Commands;

/// <summary>
/// Command bat dong bo cho WPF, tranh block UI thread khi goi network/service.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    /// <summary>
    /// Khoi tao async command.
    /// </summary>
    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Chi cho phep chay khi khong dang running va dieu kien ngoai thoa man.
    /// </summary>
    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <summary>
    /// Chay ham async va tu dong khoa command den khi xong.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        NotifyCanExecuteChanged();

        try
        {
            await _executeAsync();
        }
        catch (Exception ex)
        {
            // Guard against process termination from async-void command exceptions.
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    $"Lenh thuc thi bi loi: {ex.Message}",
                    "SpaceShip War - Command Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
        }
        finally
        {
            _isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Buoc WPF reevaluate CanExecute de cap nhat trang thai control.
    /// </summary>
    public void NotifyCanExecuteChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = dispatcher.InvokeAsync(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }
}
