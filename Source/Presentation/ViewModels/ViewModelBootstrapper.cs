namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// Diem tao ViewModel de App/MainWindow co the khoi tao 1 dong.
/// </summary>
public static class ViewModelBootstrapper
{
    /// <summary>
    /// Tao MainGameViewModel voi toan bo service mac dinh cho demo.
    /// </summary>
    public static MainGameViewModel CreateMainGameViewModel()
    {
        return MainGameViewModel.CreateDefault();
    }
}
