using UnityEngine;

/// <summary>
/// One phase-end milestone that triggers a pop-up after a specific wave clears.
///
/// ── Setup ──
///   1. Right-click in Project → Create → Click n Claw → Milestone Data.
///   2. Set Trigger After Wave (0-indexed: wave 5 = index 4, wave 10 = index 9, etc.).
///   3. Fill in Phase Name and add TroopData entries for every ally unlocked this phase.
///   4. For evolution unlocks (Fire Ant, Giant Mantis, etc.) use Evolution Unlocks instead.
///   5. Drag all MilestoneData assets into MilestonePopupController.milestones in order.
/// </summary>
[CreateAssetMenu(fileName = "NewMilestone", menuName = "Click n Claw/Milestone Data")]
public class MilestoneData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown at the top of the pop-up. e.g. 'Phase 2: Creeping Threats'")]
    public string phaseName;

    [Tooltip("0-based index of the wave that triggers this pop-up when cleared.\n" +
             "Wave 5 = 4 | Wave 10 = 9 | Wave 15 = 14 | Wave 25 = 24\n" +
             "Wave 30 = 29 | Wave 35 = 34 | Wave 40 = 39 | Wave 45 = 44\n" +
             "Wave 49 = 48 | Wave 50 (victory) = 49")]
    public int triggerAfterWave;

    [Tooltip("If true, shows the victory variant (KEEP GOING / MAIN MENU buttons, no ally list).")]
    public bool isVictory;

    [Header("Unlocked Allies — base troops")]
    [Tooltip("TroopData assets for newly unlocked troops. Stats and ability are read automatically.")]
    public TroopData[] unlockedTroops;

    [Header("Unlocked Evolutions")]
    [Tooltip("Use this for evolution unlocks (Fire Ant, Bullet Ant, Giant Mantis, Mutated Frog, etc.) " +
             "that don't have their own base TroopData asset.")]
    public EvolutionUnlockEntry[] evolutionUnlocks;
}

/// <summary>
/// Manual entry for an evolution or upgrade unlock that doesn't have a base TroopData asset.
/// </summary>
[System.Serializable]
public class EvolutionUnlockEntry
{
    [Tooltip("Portrait sprite for this evolved form.")]
    public Sprite portrait;

    public string displayName;

    [Tooltip("Short description shown under the name, e.g. 'Evolution of Ant'.")]
    public string subtitle;

    [Tooltip("One-line special ability description, e.g. 'Sets enemies on fire on hit'.")]
    public string abilityDescription;
}
