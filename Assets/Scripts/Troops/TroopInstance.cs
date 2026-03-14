using UnityEngine;

/// <summary>
/// Attach to every placed troop. Tracks upgrade level and total gold spent.
/// Requires a Collider2D so OnMouseDown fires.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TroopInstance : MonoBehaviour
{
public TroopData Data        { get; private set; }
    public int UpgradeLevel      { get; private set; }
    public int TotalGoldSpent    { get; private set; }

    public int  SellValue      => Mathf.RoundToInt(TotalGoldSpent * 0.5f);
    public bool CanUpgrade     => Data != null && UpgradeLevel < Data.upgrades.Length;
    public int  NextUpgradeCost => CanUpgrade ? Data.upgrades[UpgradeLevel].cost : 0;

    public void Initialize(TroopData data)
    {
        Data          = data;
        UpgradeLevel  = 0;
        TotalGoldSpent = data.baseCost;
    }

    /// <summary>Applies the next upgrade tier. Call after spending the gold.</summary>
    public void Upgrade()
    {
        if (!CanUpgrade) return;
        TotalGoldSpent += Data.upgrades[UpgradeLevel].cost;
        UpgradeLevel++;

        // TODO: apply stat changes here (range, damage, attack speed, etc.)
        Debug.Log($"[TroopInstance] {Data.troopName} upgraded to level {UpgradeLevel}");
    }

    /// <summary>Destroys this troop. Call after awarding the sell gold.</summary>
    public void Sell()
    {
        Destroy(gameObject);
    }

}
