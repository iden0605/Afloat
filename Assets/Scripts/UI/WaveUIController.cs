using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Updates the wave counter, animated heart HP display, 2× speed button, and the
/// between-wave countdown clock in the HUD.
///
/// ── Scene setup ──
///   1. Add a GameObject "WaveUI" to the scene.
///   2. Attach a UIDocument component → assign WaveUI.uxml, Sort Order 5.
///   3. Attach this script to the same GameObject.
///   4. Assign the 5 heart sprites (frames 0–4) to Heart Sprites in the Inspector.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WaveUIController : MonoBehaviour
{
    public static WaveUIController Instance { get; private set; }

    [Header("Heart Animation")]
    [Tooltip("Sprite frames in order: 0, 1, 2, 3, 4")]
    [SerializeField] private Sprite[] heartSprites;

    [Tooltip("Frames per second for the heart animation.")]
    [SerializeField] private float frameRate = 8f;

    // ── Private state ──────────────────────────────────────────────────────────
    private Label         _waveLabel;
    private Label         _healthLabel;
    private VisualElement _heartIcon;
    private Button        _speedBtn;

    private float _animTime = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _waveLabel   = root.Q<Label>("wave-label");
        _healthLabel = root.Q<Label>("health-label");
        _heartIcon   = root.Q("heart-icon");
        _speedBtn    = root.Q<Button>("speed-btn");

        if (_speedBtn != null) _speedBtn.clicked += OnSpeedClicked;

        PlayerHealthManager.OnHealthChanged += RefreshHealth;
        RefreshHealth();
    }

    void OnDisable()
    {
        if (_speedBtn != null) _speedBtn.clicked -= OnSpeedClicked;
        PlayerHealthManager.OnHealthChanged -= RefreshHealth;
    }

    void Update()
    {
        UpdateWaveLabel();
        UpdateSpeedButton();
        UpdateHeartAnimation();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void UpdateWaveLabel()
    {
        if (WaveManager.Instance == null || _waveLabel == null) return;
        int idx = WaveManager.Instance.CurrentWaveIndex;
        _waveLabel.text = idx < 0 ? "WAVE —" : $"WAVE {idx + 1}";
    }

    void UpdateSpeedButton()
    {
        if (_speedBtn == null) return;
        bool fast = WaveManager.Instance != null && WaveManager.Instance.IsDoubleSpeed;
        if (fast) _speedBtn.AddToClassList("hud-ctrl-btn--active");
        else      _speedBtn.RemoveFromClassList("hud-ctrl-btn--active");
    }

    void UpdateHeartAnimation()
    {
        if (_heartIcon == null || heartSprites == null || heartSprites.Length == 0) return;

        int frameCount = heartSprites.Length;
        _animTime += Time.deltaTime * frameRate;

        int pingPongLength = frameCount * 2 - 2;
        int step  = Mathf.FloorToInt(_animTime) % Mathf.Max(1, pingPongLength);
        int frame = step < frameCount ? step : pingPongLength - step;
        frame = Mathf.Clamp(frame, 0, frameCount - 1);

        var sprite = heartSprites[frame];
        if (sprite != null)
            _heartIcon.style.backgroundImage = new StyleBackground(sprite);
    }

    void RefreshHealth()
    {
        if (_healthLabel == null) return;
        _healthLabel.text = PlayerHealthManager.Instance != null
            ? PlayerHealthManager.Instance.CurrentHealth.ToString()
            : "—";
    }

    void OnSpeedClicked()
    {
        if (WaveManager.Instance == null) return;
        WaveManager.Instance.SetDoubleSpeed(!WaveManager.Instance.IsDoubleSpeed);
    }

    /// <summary>
    /// Disables all interactive HUD controls. Called on game over or victory
    /// so the player cannot change game state through the HUD.
    /// </summary>
    public void LockHUD()
    {
        if (_speedBtn != null) _speedBtn.SetEnabled(false);
    }

    // ── Hint system bounds ─────────────────────────────────────────────────────
    public Rect GetControlButtonsBounds()   => _speedBtn?.worldBound  ?? Rect.zero;
}
