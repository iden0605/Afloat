using UnityEngine;

/// <summary>
/// Scene-level singleton for assigning art assets that are referenced purely
/// from code (no prefab inspector). Add to any persistent scene GameObject
/// and drag assets in via the Inspector.
/// </summary>
public class GameAssets : MonoBehaviour
{
    public static GameAssets Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Gold Coin")]
    [Tooltip("Drag Assets/Art/Sprites/Coin.png here.")]
    public Sprite coinSprite;

    [Tooltip("World-unit diameter of the coin. Tweak freely.")]
    public float coinSize = 0.11f;
}
