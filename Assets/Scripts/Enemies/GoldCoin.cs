using UnityEngine;

/// <summary>
/// A single gold coin particle that lingers briefly at the enemy death position
/// then flies to the Gold HUD, crediting gold on arrival.
///
/// Spawned by EnemyInstance.SpawnGoldCoins(). Count is capped per enemy so
/// mass-death events stay performant.
/// </summary>
public class GoldCoin : MonoBehaviour
{
    private int     _goldAmount;
    private Vector3 _startPos;
    private Vector3 _scatterPos;
    private float   _lingerEnd;
    private float   _flyStartTime;
    private bool    _flying;

    private const float LingerDuration = 1.0f;
    private const float FlyDuration    = 0.5f;


    /// <param name="goldAmount">Gold to credit when this coin arrives at the HUD.</param>
    /// <param name="scatterOffset">World-space offset the coin drifts to during the linger phase.</param>
    public void Initialize(int goldAmount, Vector3 scatterOffset)
    {
        _goldAmount = goldAmount;
        _startPos   = transform.position;
        _scatterPos = _startPos + scatterOffset;
        _lingerEnd  = Time.time + LingerDuration;
        _flying     = false;

        BuildVisual();
    }

    // ── Visual ────────────────────────────────────────────────────────────────

    private void BuildVisual()
    {
        var assets = GameAssets.Instance;
        var sprite = assets != null ? assets.coinSprite : null;
        float size = assets != null ? assets.coinSize : 0.11f;

        var sr          = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 15;

        // Scale so the sprite renders at exactly `size` world units wide
        if (sprite != null)
        {
            float scale = (size * sprite.pixelsPerUnit) / sprite.rect.width;
            transform.localScale = Vector3.one * scale;
        }
        else
        {
            transform.localScale = Vector3.one * size;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_flying)
            UpdateLinger();
        else
            UpdateFly();
    }

    private void UpdateLinger()
    {
        float t = 1f - Mathf.Clamp01((_lingerEnd - Time.time) / LingerDuration);
        transform.position = Vector3.Lerp(_startPos, _scatterPos, EaseOut(t));

        if (Time.time >= _lingerEnd)
        {
            _flying       = true;
            _flyStartTime = Time.time;
        }
    }

    private void UpdateFly()
    {
        float t      = Mathf.Clamp01((Time.time - _flyStartTime) / FlyDuration);
        Vector3 target = BadgeWorldPos();

        transform.position = Vector3.Lerp(_scatterPos, target, EaseIn(t));

        if (t >= 1f)
        {
            GoldManager.Instance?.AddGold(_goldAmount);
            Destroy(gameObject);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Vector3 BadgeWorldPos()
    {
        if (GoldHUD.Instance != null)
            return GoldHUD.Instance.BadgeWorldPosition;

        // Fallback: top-centre of screen if HUD isn't ready
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        float z = Mathf.Abs(cam.transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.92f, z));
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseIn(float t)  => t * t;

}
