using UnityEngine;

/// <summary>
/// Attach to every placed troop. Tracks upgrade level, evolution state, and total gold spent.
/// Requires a Collider2D so raycasts can detect it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TroopInstance : MonoBehaviour
{
    public TroopData Data             { get; internal set; }
    public int       UpgradeLevel     { get; internal set; }
    public int       EvolutionLevel   { get; internal set; }  // 0 = base, 1 = first evo, etc.
    public int       TotalGoldSpent   { get; internal set; }

    public float CurrentAttack         { get; internal set; }
    public float CurrentAttackSpeed   { get; internal set; }
    public float CurrentRange         { get; internal set; }

    /// <summary>Seconds between attacks, derived from CurrentAttackSpeed. Use this in attack scripts.</summary>
    public float CurrentAttackInterval => 1f / Mathf.Max(0.01f, CurrentAttackSpeed);

    public int  SellValue       => Mathf.RoundToInt(TotalGoldSpent * 0.5f);
    public bool CanUpgrade      => Data != null && UpgradeLevel < Data.upgrades.Length;
    public int  NextUpgradeCost => CanUpgrade ? Data.upgrades[UpgradeLevel].cost : 0;

    /// <summary>The next evolution in the chain, or null if fully evolved / no evolutions.</summary>
    public EvolutionData NextEvolution => (Data != null && Data.HasEvolutions && EvolutionLevel < Data.evolutions.Length)
        ? Data.evolutions[EvolutionLevel]
        : null;

    /// <summary>True when the next evolution exists and its upgrade requirement is met.</summary>
    public bool CanEvolve => NextEvolution != null && UpgradeLevel >= NextEvolution.upgradesRequired;

    public void Initialize(TroopData data)
    {
        Data           = data;
        UpgradeLevel   = 0;
        EvolutionLevel = 0;
        TotalGoldSpent = data.baseCost;

        CurrentAttack      = data.attack;
        CurrentAttackSpeed = data.attackSpeed;
        CurrentRange       = data.range;
    }

    /// <summary>Applies the next upgrade tier's stat deltas.</summary>
    public void Upgrade()
    {
        if (!CanUpgrade) return;
        var tier = Data.upgrades[UpgradeLevel];
        TotalGoldSpent += tier.cost;
        UpgradeLevel++;

        CurrentAttack      += tier.attackDelta;
        CurrentAttackSpeed += tier.attackSpeedDelta;
        CurrentRange       += tier.rangeDelta;

        Debug.Log($"[TroopInstance] {Data.troopName} upgraded to tier {UpgradeLevel}");
    }

    /// <summary>
    /// Swaps this troop for the next evolution's prefab, carrying over all accumulated stats.
    /// Returns the new TroopInstance so callers (e.g. TroopSelectionUI) can update their reference.
    /// Returns null if CanEvolve is false or the evolution prefab is missing.
    /// </summary>
    public TroopInstance Evolve()
    {
        if (!CanEvolve) return null;

        var evo = NextEvolution;
        if (evo.prefab == null)
        {
            Debug.LogWarning($"[TroopInstance] Evolution '{evo.evolutionName}' has no prefab assigned.");
            return null;
        }

        // Spawn the evolved prefab at the same position/rotation under the same parent
        var go      = UnityEngine.Object.Instantiate(evo.prefab, transform.position, transform.rotation, transform.parent);
        var newInst = go.GetComponent<TroopInstance>() ?? go.AddComponent<TroopInstance>();

        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<CapsuleCollider2D>();
            col.isTrigger = true;
        }

        // Copy accumulated state, increment evolution level, apply this evolution's stat boosts
        newInst.Data           = Data;
        newInst.UpgradeLevel   = UpgradeLevel;
        newInst.EvolutionLevel = EvolutionLevel + 1;
        newInst.TotalGoldSpent = TotalGoldSpent;
        newInst.CurrentAttack      = CurrentAttack      + evo.attackBoost;
        newInst.CurrentAttackSpeed = CurrentAttackSpeed + evo.attackSpeedBoost;
        newInst.CurrentRange       = CurrentRange       + evo.rangeBoost;

        TroopManager.Instance.Unregister(this);
        TroopManager.Instance.Register(newInst);

        Debug.Log($"[TroopInstance] {Data.troopName} evolved into {evo.evolutionName}! (EvolutionLevel {newInst.EvolutionLevel})");

        Destroy(gameObject);
        return newInst;
    }

    /// <summary>Destroys this troop and removes it from the registry.</summary>
    public void Sell()
    {
        TroopManager.Instance.Unregister(this);
        Destroy(gameObject);
    }
}
