using System.Collections.Generic;
using System.Linq;

namespace Battleship2D.Game.Economy;
//class representing the player's current ship loadout 
//including equipped weapons and their total cost
public class ShipLoadout
{
    public string ShipName { get; set; } = "";
    public List<ShopItem> EquippedWeapons { get; set; } = new();
    
    public int CalculateCost() => EquippedWeapons.Sum(w => w.Cost);
    
    public void EquipWeapon(ShopItem weapon)
    {
        if (!EquippedWeapons.Any(w => w.Id == weapon.Id))
            EquippedWeapons.Add(weapon);
    }
    //method to unequip a weapon from the ship loadout, removing it from the list of equipped weapons
    public void UnequipWeapon(ShopItem weapon)
    {
        EquippedWeapons.RemoveAll(w => w.Id == weapon.Id);
    }
    //method to check if a weapon can be equipped based on ship size and maximum cost constraints
    public bool CanEquip(ShopItem weapon, ShipSize shipSize, int maxPerShip) 
    {
        if (!weapon.AllowedShipSizes.Contains(shipSize))
            return false;
        
        int newCost = CalculateCost() + weapon.Cost;
        return newCost <= maxPerShip;
    }
}