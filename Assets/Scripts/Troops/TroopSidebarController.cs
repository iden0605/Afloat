using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reads the TroopData list and builds the sidebar cards at runtime.
/// To add a new troop: create a TroopData asset and drag it into the Troops list here.
/// Must be on the same GameObject as the UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TroopSidebarController : MonoBehaviour
{
    [Tooltip("One TroopData asset per troop type. Order matches display order.")]
    [SerializeField] private List<TroopData> troops = new();

    private VisualElement _sidebar;
    private Button        _toggleBtn;
    private bool          _isOpen;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _sidebar   = root.Q("sidebar-container");
        _toggleBtn = root.Q<Button>("toggle-btn");
        _toggleBtn.clicked += ToggleSidebar;

        BuildCards(root.Q("troop-list"));
    }

    void OnDisable()
    {
        if (_toggleBtn != null) _toggleBtn.clicked -= ToggleSidebar;
    }

    // -------------------------------------------------------
    // Card building
    // -------------------------------------------------------

    void BuildCards(VisualElement list)
    {
        list.Clear();
        foreach (var data in troops)
        {
            if (data == null) continue;
            list.Add(MakeCard(data));
        }
    }

    VisualElement MakeCard(TroopData data)
    {
        // Card container — column layout: portrait on top, cost below
        var card = new VisualElement();
        card.AddToClassList("troop-card");
        card.tooltip = data.troopName;

        // Portrait thumbnail
        var portrait = new VisualElement();
        portrait.AddToClassList("troop-portrait");
        if (data.portrait != null)
            portrait.style.backgroundImage = new StyleBackground(data.portrait);

        // Cost label (gold / yellow text)
        var costLabel = new Label($"{data.baseCost}g");
        costLabel.AddToClassList("troop-cost");

        card.Add(portrait);
        card.Add(costLabel);

        // Drag-to-place: begin drag on pointer down (not on click release)
        var captured = data;
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            evt.StopPropagation();
            TroopDragController.Instance.BeginNewDrag(captured);
        });

        return card;
    }

    // -------------------------------------------------------
    // Toggle
    // -------------------------------------------------------

    void ToggleSidebar()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _sidebar.AddToClassList("open");
            _toggleBtn.text = "\u2715"; // ✕
        }
        else
        {
            _sidebar.RemoveFromClassList("open");
            _toggleBtn.text = "\u2630"; // ☰
        }
    }
}
