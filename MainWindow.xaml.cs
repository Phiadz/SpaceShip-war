using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battleship2D.Presentation.ViewModels;

namespace SpaceShipWar;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryApplyBackground();
        DataContext = ViewModelBootstrapper.CreateMainGameViewModel();
    }

    private void TryApplyBackground()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "space_breaker_asset", "Background", "galaxy.png");
            if (!File.Exists(path))
            {
                return;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            RootGrid.Background = new ImageBrush(image)
            {
                Opacity = 0.15,
                Stretch = Stretch.UniformToFill
            };
        }
        catch
        {
            // Keep default background if image loading fails.
        }
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
