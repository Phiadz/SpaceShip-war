using System;
using System.Threading.Tasks;
using System.Windows;
using Battleship2D.Presentation.ViewModels;

namespace SpaceShipWar;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModelBootstrapper.CreateMainGameViewModel();
    }

    private async void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IAsyncDisposable asyncDisposable)
        {
            try
            {
                await asyncDisposable.DisposeAsync();
            }
            catch
            {
                // Ignore dispose errors during app shutdown.
            }
        }
    }
}
