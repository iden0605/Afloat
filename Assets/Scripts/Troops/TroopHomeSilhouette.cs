using UnityEngine;

/// <summary>
/// Add to any melee troop prefab that physically moves to attack.
/// While the troop is away from its home position the sprite becomes unclickable
/// and a dark ghost appears at the home tile instead. The ghost is what the player
/// clicks to open the selection panel and range indicator.
/// When the troop returns home the ghost disappears and the normal collider is re-enabled.
/// </summary>
public class TroopHomeSilhouette : MonoBehaviour
{
    [Tooltip("Distance (world units) the troop must travel before the silhouette appears.")]
    [SerializeField] private float moveThreshold = 0.05f;

    [Tooltip("Color applied to the ghost sprite — typically a dark, semi-transparent black.")]
    [SerializeField] private Color silhouetteColor = new Color(0f, 0f, 0f, 0.45f);

    public Vector3 HomePosition => _homePos;

    private TroopInstance  _instance;
    private Collider2D     _troopCollider;
    private SpriteRenderer _troopSR;
    private Vector3        _homePos;

    private GameObject     _ghostGO;
    private SpriteRenderer _ghostSR;
    private bool           _silhouetteActive;

    void Start()
    {
        _instance      = GetComponent<TroopInstance>();
        _troopCollider = GetComponent<Collider2D>();
        _homePos       = transform.position;

        // Find the primary sprite renderer, skipping health-bar children
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            var n = sr.gameObject.name;
            if (n == "HP_BG" || n == "HP_Fill") continue;
            _troopSR = sr;
            break;
        }

        BuildGhost();
    }

    void BuildGhost()
    {
        _ghostGO = new GameObject("TroopSilhouette");
        _ghostGO.transform.position = _homePos;

        _ghostSR = _ghostGO.AddComponent<SpriteRenderer>();
        if (_troopSR != null)
        {
            _ghostSR.sprite           = _troopSR.sprite;
            _ghostSR.sortingLayerName = _troopSR.sortingLayerName;
            _ghostSR.sortingOrder     = _troopSR.sortingOrder;
            _ghostSR.flipX            = _troopSR.flipX;
        }
        _ghostSR.color = silhouetteColor;

        // Collider sized to match the troop's own collider
        var col = _ghostGO.AddComponent<CapsuleCollider2D>();
        col.isTrigger = true;
        if (_troopCollider is CapsuleCollider2D cap)
        {
            col.size      = cap.size;
            col.direction = cap.direction;
        }
        else if (_troopCollider is BoxCollider2D box)
        {
            col.size = box.size;
        }
        else
        {
            col.size = new Vector2(0.4f, 0.4f);
        }

        // Proxy lets TroopManager's raycast resolve to the real TroopInstance
        _ghostGO.AddComponent<TroopHomeProxy>().Init(_instance);
        _ghostGO.SetActive(false);
    }

    void Update()
    {
        if (_ghostGO == null || _troopSR == null) return;

        bool awayFromHome = Vector3.Distance(transform.position, _homePos) > moveThreshold;

        if (awayFromHome != _silhouetteActive)
        {
            _silhouetteActive = awayFromHome;
            _ghostGO.SetActive(awayFromHome);
            if (_troopCollider != null)
                _troopCollider.enabled = !awayFromHome;
        }

        // Mirror the current animation frame so the silhouette isn't stuck on one pose
        if (_silhouetteActive)
        {
            _ghostSR.sprite = _troopSR.sprite;
            _ghostSR.flipX  = _troopSR.flipX;
        }
    }

    void OnDestroy()
    {
        if (_ghostGO != null)
            Destroy(_ghostGO);
    }
}

/// <summary>
/// Placed on the silhouette ghost GameObject so TroopManager's raycast can
/// resolve back to the real TroopInstance without it being on the ghost itself.
/// </summary>
public class TroopHomeProxy : MonoBehaviour
{
    public TroopInstance Troop { get; private set; }
    public void Init(TroopInstance troop) => Troop = troop;
}
