using System.ComponentModel;
using System.Linq;

namespace Battleship2D.Game.Economy;
//static class to hold the catalog of available weapons in the game
//providing methods to retrieve them by ID or ship size
public static class WeaponCatalog
{
    private const string AssetsRoot = "Assets";
    private const string AssetPackFolder = "space_breaker_asset";
    private const string WeaponsFolder = "Weapons";
    
    public static readonly ShopItem[] Weapons = new[]
    {
        new ShopItem 
        { 
            Id = 1001, 
            Name = "Small Laser Cannon", 
            Cost = 20, 
            Category = "Weapon",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, WeaponsFolder, "Small", "Laser", "turret_02_mk1.png" },
            Description = "A small but highly precise weapon that fires concentrated laser beams with moderate damage and high accuracy. Effective against small, agile targets but less effective against heavily armored ships, can be blocked by shields",
            AllowedShipSizes = new[] { ShipSize.Small, ShipSize.Medium }
        },
        new ShopItem 
        { 
            Id = 1002, 
            Name = "Plasma Blaster v1", 
            Cost = 40, 
            Category = "Weapon",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, WeaponsFolder, "Medium", "Plasma", "turret_04_mk1.png" },
            Description = "Powerful energy weapon that fires plasma bolts with high damage potential but moderate accuracy and fire rate. Effective against medium and large ships, but less effective against small, agile targets, destroy enemy shields if hits, but has a chance to miss due to plasma dispersion.",
            AllowedShipSizes = new[] { ShipSize.Medium, ShipSize.Big }
        },
        new ShopItem 
        { 
            Id = 1003, 
            Name = "Small Shield Generator", 
            Cost = 30, 
            Category = "Support",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, "Others","Shield", "Shield.png" },
            Description = "Defense system that absorbs 1x1 tile hits, can be destroyed by enemy fire, but can regenerate over time if not taking damage. Provides an extra layer of protection for small ships, but has limited durability and is less effective against sustained fire, a small weapon module can be placed on it top to allow it to attack while active",
            AllowedShipSizes = new[] { ShipSize.Small, ShipSize.Medium, ShipSize.Big }
        },
        new ShopItem 
        { 
            Id = 1004, 
            Name = "Rapid Fire Cannon v1", 
            Cost = 30, 
            Category = "Weapon",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, WeaponsFolder, "Medium", "Cannon", "turret_01_mk1.png" },
            Description = "A high rate of fire canon that fires conventional projectiles, can be be used for scouting out enemy positions and dealing consistent damage, can be blocked by shields ",
            AllowedShipSizes = new[] { ShipSize.Medium, ShipSize.Big }
        },
        new ShopItem 
        { 
            Id = 1005,
            Name = "Heavy Missile Launcher",
            Cost = 70,
            Category = "Weapon",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, WeaponsFolder, "Big", "Missile", "turret_03_mk3.png" },
            Description = "Powerful missile launcher that fires guided missiles with high damage and area of effect, effective against large ships and groups of smaller ships, but is expensive and has a slow rate of fire, can be blocked by shields",
            AllowedShipSizes = new[] { ShipSize.Big }
        },
        new ShopItem 
        { 
            Id = 1006,
            Name = "Repair Drone Bay",
            Cost = 40,
            Category = "Support",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, "Others", "Drone", "bottom_01_stay.png" },
            Description = "Automated repair system that deploys drones to repair 1x1 tiles on the ship and other ships, can be targeted and destroyed by enemy fire, but can provide crucial repairs during battle if protected, effective for keeping larger ships in the fight longer, but requires careful positioning and protection to be effective",
            AllowedShipSizes = new[] { ShipSize.Big }
        },
        new ShopItem
        { 
            Id = 1007,
            Name = "Small Cannon Turret",
            Cost = 10,
            Category = "Weapon",
            AssetPathParts = new[] { AssetsRoot, AssetPackFolder, WeaponsFolder, "Small", "Cannon", "turret_01_mk1.png" },
            Description = "A basic cannon turret for small ships, providing light offensive capability.",
            AllowedShipSizes = new[] { ShipSize.Small, ShipSize.Medium, ShipSize.Big }
        },
    };
    
    public static ShopItem? GetWeapon(int id) => Weapons.FirstOrDefault(w => w.Id == id);
    public static ShopItem[] GetWeaponsBySize(ShipSize size) => Weapons.Where(w => w.AllowedShipSizes.Contains(size)).ToArray();
}