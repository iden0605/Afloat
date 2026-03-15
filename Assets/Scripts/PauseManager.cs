using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Handles the in-game pause overlay.
///
/// ── Scene setup ──
///   1. Add this component to a new GameObject named "PauseManager".
///   2. Add a UIDocument component to the same GameObject.
///   3. Assign PauseMenu.uxml as the Source Asset on the UIDocument.
///   4. Set the UIDocument Sort Order higher than all other UIDocuments
///      (e.g. 10) so the pause panel renders on top.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    /// <summary>True while the game is paused.</summary>
    public bool IsPaused { get; private set; } = false;

    // Remembered so we can restore double-speed correctly on resume.
    private bool _wasDoubleSpeed = false;

    private VisualElement _pauseRoot;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _pauseRoot = root.Q("pause-root");

        root.Q<Button>("continue-btn").clicked += OnContinue;
        root.Q<Button>("restart-btn") .clicked += OnRestart;
        root.Q<Button>("quit-btn")    .clicked += OnQuit;
    }

    void OnDisable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;

        var continueBtn = root.Q<Button>("continue-btn");
        if (continueBtn != null) continueBtn.clicked -= OnContinue;

        var restartBtn = root.Q<Button>("restart-btn");
        if (restartBtn != null) restartBtn.clicked -= OnRestart;

        var quitBtn = root.Q<Button>("quit-btn");
        if (quitBtn != null) quitBtn.clicked -= OnQuit;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        // Remember double-speed state, then freeze time.
        _wasDoubleSpeed = WaveManager.Instance != null && WaveManager.Instance.IsDoubleSpeed;
        Time.timeScale = 0f;

        _pauseRoot.style.display = DisplayStyle.Flex;
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        // Restore previous time scale.
        Time.timeScale = _wasDoubleSpeed ? 2f : 1f;

        _pauseRoot.style.display = DisplayStyle.None;
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else          Pause();
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    void OnContinue() => Resume();

    void OnRestart()
    {
        // Restore time scale before reloading so the new scene starts normally.
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnQuit()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene("MainMenu");
    }
}
