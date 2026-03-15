using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Drives the main menu UI Document.
/// Assign this component (alongside a UIDocument) to a GameObject in the MainMenu scene.
/// Drag the corresponding sprites into each SerializeField slot in the Inspector.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [Header("Background")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Centipede Decoration (hangs off Play button)")]
    [SerializeField] private Sprite centipedeSprite;

    [Header("Critter Icons (bottom row)")]
    [SerializeField] private Sprite frogSprite;
    [SerializeField] private Sprite antSprite;
    [SerializeField] private Sprite beetleSprite;
    [SerializeField] private Sprite mantisSprite;

    [Header("Enemy Icons (water strip)")]
    [SerializeField] private Sprite plasticBagSprite;
    [SerializeField] private Sprite mosquitoSprite;
    [SerializeField] private Sprite strawSprite;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // ── Sprite assignments ─────────────────────────────────
        SetBg(root, "bg-image",        backgroundSprite);
        SetBg(root, "centipede-deco",  centipedeSprite);
        SetBg(root, "deco-frog",        frogSprite);
        SetBg(root, "deco-ant",         antSprite);
        SetBg(root, "deco-beetle",      beetleSprite);
        SetBg(root, "deco-mantis",      mantisSprite);
        SetBg(root, "deco-plastic-bag", plasticBagSprite);
        SetBg(root, "deco-mosquito",    mosquitoSprite);
        SetBg(root, "deco-straw",       strawSprite);

        // ── Button callbacks ───────────────────────────────────
        root.Q<Button>("play-btn")    .clicked += OnPlay;
        root.Q<Button>("tutorial-btn").clicked += OnTutorial;
        root.Q<Button>("exit-btn")    .clicked += OnExit;
    }

    void OnDisable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;

        var playBtn = root.Q<Button>("play-btn");
        if (playBtn != null) playBtn.clicked -= OnPlay;

        var tutBtn = root.Q<Button>("tutorial-btn");
        if (tutBtn != null) tutBtn.clicked -= OnTutorial;

        var exitBtn = root.Q<Button>("exit-btn");
        if (exitBtn != null) exitBtn.clicked -= OnExit;
    }

    // ── Handlers ────────────────────────────────────────────────

    void OnPlay()     => SceneManager.LoadScene("Main");
    void OnTutorial() => SceneManager.LoadScene("Main"); // TODO: replace with "Tutorial" scene when ready
    void OnExit()     => Application.Quit();

    // ── Helper ──────────────────────────────────────────────────

    static void SetBg(VisualElement root, string elementName, Sprite sprite)
    {
        if (sprite == null) return;
        var el = root.Q(elementName);
        if (el != null)
            el.style.backgroundImage = new StyleBackground(sprite);
    }
}
