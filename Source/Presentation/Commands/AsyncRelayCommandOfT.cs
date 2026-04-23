using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Battleship2D.Presentation.Commands;

/// <summary>
/// Class hỗ trợ chạy lệnh Async có nhận tham số truyền vào (Ví dụ: truyền ShopItem khi double click).
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting) return false;
        
        if (parameter is null)
        {
            return _canExecute?.Invoke(default) ?? true;
        }
        
        if (parameter is T tParam)
        {
            return _canExecute?.Invoke(tParam) ?? true;
        }

        return false;
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            T? arg = parameter is T t ? t : default;
            await _execute(arg);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}