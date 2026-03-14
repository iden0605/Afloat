using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and manages the floating Upgrade / Move / Sell popup that appears
/// above a selected troop. Must be on the same GameObject as the UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TroopSelectionUI : MonoBehaviour
{
    public static TroopSelectionUI Instance { get; private set; }

    private UIDocument    _uiDoc;
    private TroopInstance _target;

    // Popup elements (built entirely in C#)
    private VisualElement _popup;
    private Button        _upgradeBtn;
    private Label         _upgradeCostLabel;
    private Label         _sellValueLabel;
    private Label         _troopNameLabel;

    private const float PopupHalfWidth = 90f;

    // Used to prevent TroopManager from re-opening the popup on the same frame Hide() is called
    private int _hideFrame = -1;
    public bool JustHidden => Time.frameCount == _hideFrame;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _uiDoc = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        BuildPopup();
        Hide();
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    public void Show(TroopInstance troop)
    {
        _target = troop;
        _troopNameLabel.text = troop.Data.troopName;
        Refresh();
        _popup.RemoveFromClassList("sel-hidden");
    }

    public void Hide()
    {
        _hideFrame = Time.frameCount;
        _target = null;
        _popup?.AddToClassList("sel-hidden");
    }

    public void Refresh()
    {
        if (_target == null) return;

        bool canUpgrade = _target.CanUpgrade;
        _upgradeBtn.SetEnabled(canUpgrade);
        _upgradeCostLabel.text = canUpgrade ? $"{_target.NextUpgradeCost}g" : "MAX";
        _sellValueLabel.text   = $"{_target.SellValue}g back";
    }

    // -------------------------------------------------------
    // Popup construction
    // -------------------------------------------------------

    void BuildPopup()
    {
        _popup = new VisualElement();
        _popup.AddToClassList("sel-popup");

        // ── Close button (top-right) ──────────────────────
        var closeBtn = new Button(Hide) { text = "×" };
        closeBtn.AddToClassList("sel-close");
        _popup.Add(closeBtn);

        // ── Troop name ────────────────────────────────────
        _troopNameLabel = new Label();
        _troopNameLabel.AddToClassList("sel-troop-name");
        _popup.Add(_troopNameLabel);

        // ── Button row ────────────────────────────────────
        var row = new VisualElement();
        row.AddToClassList("sel-row");

        // Upgrade group
        var upgradeGroup = new VisualElement();
        upgradeGroup.AddToClassList("sel-group");
        _upgradeBtn = new Button(OnUpgradeClicked) { text = "▲" };
        _upgradeBtn.AddToClassList("sel-btn");
        _upgradeBtn.AddToClassList("sel-upgrade");
        _upgradeCostLabel = new Label("??g");
        _upgradeCostLabel.AddToClassList("sel-sub");
        upgradeGroup.Add(_upgradeBtn);
        upgradeGroup.Add(_upgradeCostLabel);

        // Move group
        var moveGroup = new VisualElement();
        moveGroup.AddToClassList("sel-group");
        var moveBtn = new Button(OnMoveClicked) { text = "\u271B" }; // ✛ open centre cross = 4-directional move
        moveBtn.AddToClassList("sel-btn");
        moveBtn.AddToClassList("sel-move");
        moveGroup.Add(moveBtn);

        // Sell group
        var sellGroup = new VisualElement();
        sellGroup.AddToClassList("sel-group");
        var sellBtn = new Button(OnSellClicked) { text = "$" };
        sellBtn.AddToClassList("sel-btn");
        sellBtn.AddToClassList("sel-sell");
        _sellValueLabel = new Label("??g");
        _sellValueLabel.AddToClassList("sel-sub");
        sellGroup.Add(sellBtn);
        sellGroup.Add(_sellValueLabel);

        row.Add(upgradeGroup);
        row.Add(moveGroup);
        row.Add(sellGroup);
        _popup.Add(row);

        // Add to root last so it renders on top of sidebar
        _uiDoc.rootVisualElement.Add(_popup);
    }

    // -------------------------------------------------------
    // Positioning (runs every late update while a troop is selected)
    // -------------------------------------------------------

    void LateUpdate()
    {
        if (_target == null || _popup.ClassListContains("sel-hidden")) return;

        // Position popup just above the sprite's top edge.
        var sr = _target.GetComponent<SpriteRenderer>();
        float spriteTopY = sr != null
            ? _target.transform.position.y + sr.bounds.extents.y
            : _target.transform.position.y + 0.5f;

        var worldTop = new Vector3(_target.transform.position.x, spriteTopY, 0f);
        var panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                           _uiDoc.rootVisualElement.panel, worldTop, Camera.main);

        // Use actual resolved height once layout has run, otherwise fall back to estimate.
        float popupH = _popup.resolvedStyle.height > 0 ? _popup.resolvedStyle.height : 110f;

        _popup.style.left = panelPos.x - PopupHalfWidth;
        _popup.style.top  = panelPos.y - popupH - 4f;
    }

    // -------------------------------------------------------
    // Button callbacks
    // -------------------------------------------------------

    void OnUpgradeClicked()
    {
        if (_target == null) return;
        _target.Upgrade();
        Refresh();
    }

    void OnMoveClicked()
    {
        if (_target == null) return;
        var t = _target;
        Hide(); // hide first — BeginMoveDrag may re-select immediately
        TroopDragController.Instance.BeginMoveDrag(t);
    }

    void OnSellClicked()
    {
        if (_target == null) return;
        // TODO: award _target.SellValue gold to the player wallet here
        Debug.Log($"[TroopSelectionUI] Sold {_target.Data.troopName} for {_target.SellValue}g");
        _target.Sell();
        Hide();
    }
}
