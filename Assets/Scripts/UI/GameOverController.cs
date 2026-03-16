using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controls the Game Over / Victory overlay.
/// Hidden by default; shown automatically when PlayerHealthManager fires OnGameOver
/// or WaveManager fires OnGameWon.
///
/// ── Scene setup ──
///   1. Add a GameObject "GameOverUI" to the scene.
///   2. Attach a UIDocument component → assign GameOverPanel.uxml, Sort Order 15.
///   3. Attach this script to the same GameObject.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverController : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _panel;
    private Label         _title;
    private Button        _quitBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        var doc = GetComponent<UIDocument>().rootVisualElement;
        _root    = doc.Q("gameover-root");
        _panel   = doc.Q("gameover-panel");
        _title   = doc.Q<Label>("gameover-title");
        _quitBtn = doc.Q<Button>("quit-btn");

        _root.style.display = DisplayStyle.None;

        if (_quitBtn != null)
            _quitBtn.clicked += OnQuit;

        PlayerHealthManager.OnGameOver += ShowGameOver;
        WaveManager.OnGameWon          += ShowVictory;
    }

    void OnDisable()
    {
        if (_quitBtn != null)
            _quitBtn.clicked -= OnQuit;

        PlayerHealthManager.OnGameOver -= ShowGameOver;
        WaveManager.OnGameWon          -= ShowVictory;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    void ShowGameOver()
    {
        if (_title != null)
        {
            _title.text = "GAME OVER";
            _title.RemoveFromClassList("go-title--win");
        }
        _panel?.RemoveFromClassList("go-panel--win");

        Show();
    }

    void ShowVictory()
    {
        if (_title != null)
        {
            _title.text = "YOU WIN!";
            _title.AddToClassList("go-title--win");
        }
        _panel?.AddToClassList("go-panel--win");

        Show();
    }

    void Show()
    {
        _root.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;
        WaveUIController.Instance?.LockHUD();
    }

    void OnQuit()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
