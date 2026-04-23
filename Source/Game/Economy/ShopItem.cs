using System;
using System.IO;
using System.Linq;
namespace Battleship2D.Game.Economy;

public enum ShipSize { Small, Medium, Big }
//class representing an item available for purchase in the in-game shop 
//with properties for cost, category, and restrictions
public class ShopItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Category { get; set; } = "Weapon"; 
    //path to the asset representing this item in the game, used for display and instantiation
    public string[] AssetPathParts { get; set; } = new[] { "Assets", "space_breaker_asset", "Weapons", "Small" };
    //computed property to get the full path to the item's image asset, combining the base directory with the specified path parts
    public string ImagePath => Path.Combine(new[] { AppContext.BaseDirectory }.Concat(AssetPathParts).ToArray());
    public string Description { get; set; } = "";
    //maximum number of this item that can be purchased in a single match
    // used to enforce limits on powerful items
    public int MaxPerMatch { get; set; } = 1;
    //array of ship sizes that are allowed to equip this item
    //used to restrict certain items to specific ship types
    public ShipSize[] AllowedShipSizes { get; set; } = new[] { ShipSize.Small, ShipSize.Medium, ShipSize.Big };
    
    public override string ToString() => $"{Name} ({Cost} credits)";
}