using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battleship2D.Presentation.ViewModels;

namespace SpaceShipWar;

public partial class MainWindow : Window
{
    private const string DragShipNameFormat = "SpaceShipWar/ShipName";

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

    private void FleetAsset_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (DataContext is not MainGameViewModel vm)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not FleetAssetViewModel fleetAsset)
        {
            return;
        }

        if (!vm.CanDragShip(fleetAsset.Name))
        {
            return;
        }

        var data = new DataObject(DragShipNameFormat, fleetAsset.Name);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    private void BoardCell_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not MainGameViewModel vm ||
            sender is not Button button ||
            button.DataContext is not BoardCellViewModel cell)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var shipName = e.Data.GetData(DragShipNameFormat) as string;
        var canDrop = !cell.IsEnemyBoard && !string.IsNullOrWhiteSpace(shipName) && vm.CanDragShip(shipName);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void BoardCell_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainGameViewModel vm ||
            sender is not Button button ||
            button.DataContext is not BoardCellViewModel cell ||
            cell.IsEnemyBoard)
        {
            e.Handled = true;
            return;
        }

        var shipName = e.Data.GetData(DragShipNameFormat) as string;
        if (string.IsNullOrWhiteSpace(shipName))
        {
            e.Handled = true;
            return;
        }

        vm.TryPlaceShipByNameAt(shipName, cell.X, cell.Y);
        e.Handled = true;
    }
}
