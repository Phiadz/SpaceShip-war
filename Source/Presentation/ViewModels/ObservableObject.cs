using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// Base class cho ViewModel, cung cap INotifyPropertyChanged.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Helper cap nhat field + phat PropertyChanged neu gia tri thay doi.
    /// </summary>
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
        {
            return false;
        }

        backingField = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Phat su kien thay doi property cho WPF binding.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var args = new PropertyChangedEventArgs(propertyName);
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, args);
            return;
        }

        _ = dispatcher.InvokeAsync(() => PropertyChanged?.Invoke(this, args));
    }
}
