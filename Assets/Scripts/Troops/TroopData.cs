using UnityEngine;

/// <summary>
/// Create one TroopData asset per troop type via:
/// Right-click in Project → Create → Click n Claw → Troop Data
/// </summary>
[CreateAssetMenu(fileName = "NewTroopData", menuName = "Click n Claw/Troop Data")]
public class TroopData : ScriptableObject
{
    [Header("Identity")]
    public string troopName;
    public Sprite portrait;
    public GameObject prefab;

    [Header("Economy")]
    public int baseCost = 50;

    [Header("Upgrades — add one entry per upgrade tier")]
    public UpgradeTier[] upgrades;

    [System.Serializable]
    public struct UpgradeTier
    {
        public string description;
        public int cost;
    }
}
