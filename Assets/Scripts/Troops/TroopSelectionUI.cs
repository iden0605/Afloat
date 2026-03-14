using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TroopSelectionUI : MonoBehaviour
{
    public static TroopSelectionUI Instance { get; private set; }

    private UIDocument    _uiDoc;
    private TroopInstance _target;

    private VisualElement _popup;
    private Button        _upgradeBtn;
    private Label         _upgradeCostLabel;
    private Label         _sellValueLabel;
    private Label         _troopNameLabel;

    private const float PopupHalfWidth = 90f;

    // Prevents LateUpdate from closing the popup on the same frame Show() was called
    private int _showFrame = -1;

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
        _showFrame = Time.frameCount;
        _target = troop;
        _troopNameLabel.text = troop.Data.troopName;
        Refresh();
        _popup.RemoveFromClassList("sel-hidden");

        var ind = _target.GetComponentInChildren<RangeIndicator>();
        if (ind != null) { ind.SetRadius(_target.CurrentRange); ind.SetVisible(true); }
    }

    public void Hide()
    {
        _target?.GetComponentInChildren<RangeIndicator>()?.SetVisible(false);
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

        // Troop name
        _troopNameLabel = new Label();
        _troopNameLabel.AddToClassList("sel-troop-name");
        _popup.Add(_troopNameLabel);

        // Button row
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
        var moveBtn = new Button(OnMoveClicked) { text = "\u271B" };
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

        _uiDoc.rootVisualElement.Add(_popup);
    }

    // -------------------------------------------------------
    // LateUpdate — positioning + click-outside-to-close
    // -------------------------------------------------------

    void LateUpdate()
    {
        if (_popup.ClassListContains("sel-hidden")) return;

        // ── Position above sprite ──────────────────────────
        var sr = _target.GetComponent<SpriteRenderer>();
        float spriteTopY = sr != null
            ? _target.transform.position.y + sr.bounds.extents.y
            : _target.transform.position.y + 0.5f;

        var worldTop = new Vector3(_target.transform.position.x, spriteTopY, 0f);
        var panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                           _uiDoc.rootVisualElement.panel, worldTop, Camera.main);

        float popupH = _popup.resolvedStyle.height > 0 ? _popup.resolvedStyle.height : 110f;
        float popupW = PopupHalfWidth * 2f;

        var  root       = _uiDoc.rootVisualElement;
        float panelW    = root.resolvedStyle.width;
        float panelH    = root.resolvedStyle.height;
        const float pad = 6f;

        float left = Mathf.Clamp(panelPos.x - PopupHalfWidth, pad, panelW - popupW - pad);
        float top  = panelPos.y - popupH - 4f;

        // If there isn't enough room above the sprite, flip below it instead
        if (top < pad)
        {
            var worldBottom = new Vector3(_target.transform.position.x,
                _target.transform.position.y - (sr != null ? sr.bounds.extents.y : 0.5f), 0f);
            var belowPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                               _uiDoc.rootVisualElement.panel, worldBottom, Camera.main);
            top = belowPos.y + 4f;
        }

        top = Mathf.Clamp(top, pad, panelH - popupH - pad);

        _popup.style.left = left;
        _popup.style.top  = top;

        // ── Click-outside-to-close ─────────────────────────
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount == _showFrame) return; // just opened this frame — don't close

        // Convert mouse position to panel space and check against popup bounds
        float px = (Input.mousePosition.x / Screen.width)                    * root.resolvedStyle.width;
        float py = ((Screen.height - Input.mousePosition.y) / Screen.height) * root.resolvedStyle.height;

        if (!_popup.worldBound.Contains(new Vector2(px, py)))
            Hide();
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
        Hide();
        TroopDragController.Instance.BeginMoveDrag(t);
    }

    void OnSellClicked()
    {
        if (_target == null) return;
        Debug.Log($"[TroopSelectionUI] Sold {_target.Data.troopName} for {_target.SellValue}g");
        _target.Sell();
        Hide();
    }
}
