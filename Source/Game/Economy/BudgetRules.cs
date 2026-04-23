namespace Battleship2D.Game.Economy;
//class to define the rules and parameters of the in-game economy system
//such as starting budget, rewards, and limits on purchases
public class BudgetRules
{
    public int StartBudget { get; set; } = 100;
    public int MaxPerShip { get; set; } = 50;
    public int MaxPerMatch { get; set; } = 100;
    public int HitReward { get; set; } = 10;
    public int SunkReward { get; set; } = 25;
    public int WinBonus { get; set; } = 50;
    
    public static BudgetRules Default() => new();
}