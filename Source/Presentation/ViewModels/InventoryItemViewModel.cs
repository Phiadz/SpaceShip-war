namespace Battleship2D.Presentation.ViewModels;
// File này lưu thông tin về một món đồ trong kho của người chơi, 
//bao gồm cả thông tin về món đồ đó (được đại diện bởi ShopItem) và trạng thái có được trang bị hay không (IsEquipped).
public class InventoryItemViewModel : ObservableObject
{
    private bool _isEquipped;
    public Battleship2D.Game.Economy.ShopItem Item { get; init; } = null!;

    public bool IsEquipped
    {
        get => _isEquipped;
        set => SetProperty(ref _isEquipped, value);
    }
}