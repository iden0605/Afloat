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

    public float CurrentAttack      { get; private set; }
    public float CurrentAttackSpeed { get; private set; }
    public float CurrentRange       { get; private set; }

    public int  SellValue      => Mathf.RoundToInt(TotalGoldSpent * 0.5f);
    public bool CanUpgrade     => Data != null && UpgradeLevel < Data.upgrades.Length;
    public int  NextUpgradeCost => CanUpgrade ? Data.upgrades[UpgradeLevel].cost : 0;

    public void Initialize(TroopData data)
    {
        Data           = data;
        UpgradeLevel   = 0;
        TotalGoldSpent = data.baseCost;

        CurrentAttack      = data.attack;
        CurrentAttackSpeed = data.attackSpeed;
        CurrentRange       = data.range;
    }

    /// <summary>Applies the next upgrade tier. Call after spending the gold.</summary>
    public void Upgrade()
    {
        if (!CanUpgrade) return;
        var tier = Data.upgrades[UpgradeLevel];
        TotalGoldSpent += tier.cost;
        UpgradeLevel++;

        CurrentAttack      += tier.attackDelta;
        CurrentAttackSpeed += tier.attackSpeedDelta;
        CurrentRange       += tier.rangeDelta;

        Debug.Log($"[TroopInstance] {Data.troopName} upgraded to level {UpgradeLevel}");
    }

    /// <summary>Destroys this troop. Call after awarding the sell gold.</summary>
    public void Sell()
    {
        TroopManager.Instance.Unregister(this);
        Destroy(gameObject);
    }

}
