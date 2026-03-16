using UnityEngine;

/// <summary>
/// Spawns blue downward-arrow VFX above a slowed enemy.
/// Call EnemySlowIndicator.Apply(enemy, duration) from any slow source.
/// Each arrow is a LineRenderer chevron that falls and fades in world space.
/// </summary>
public class EnemySlowIndicator : MonoBehaviour
{
    private float _endTime;
    private float _nextSpawn;

    private const float SpawnInterval  = 0.32f;
    private const float SpawnHeightMin = 0.18f;
    private const float SpawnHeightMax = 0.45f;
    private const float SpawnXRange    = 0.18f;

    // ── API ───────────────────────────────────────────────────────────────────

    public static void Apply(GameObject enemy, float duration)
    {
        var ind = enemy.GetComponent<EnemySlowIndicator>();
        if (ind == null) ind = enemy.AddComponent<EnemySlowIndicator>();
        ind.Refresh(duration);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    public void Refresh(float duration)
    {
        float newEnd = Time.time + duration;
        if (newEnd > _endTime) _endTime = newEnd;
        // Spawn immediately so there's no visual lag
        _nextSpawn = Time.time;
    }

    void Update()
    {
        if (Time.time > _endTime) return;

        if (Time.time >= _nextSpawn)
        {
            SpawnArrow();
            _nextSpawn = Time.time + SpawnInterval;
        }
    }

    void SpawnArrow()
    {
        float xOffset      = Random.Range(-SpawnXRange, SpawnXRange);
        float heightOffset = Random.Range(SpawnHeightMin, SpawnHeightMax);

        var go = new GameObject("SlowArrow");
        go.transform.position = transform.position + new Vector3(xOffset, heightOffset, 0f);
        go.AddComponent<SlowArrowVFX>().Init(transform, xOffset, heightOffset);
    }
}

/// <summary>
/// A single downward-arrow chevron that drifts down and fades over its lifetime.
/// Self-destructs when done — no pooling needed for this subtle effect.
/// </summary>
public class SlowArrowVFX : MonoBehaviour
{
    private LineRenderer _lr;
    private float        _timer;
    private Transform    _enemy;       // enemy to track — arrow follows its XY position
    private float        _xOffset;    // fixed horizontal offset from enemy centre
    private float        _fallOffset; // current vertical offset, decreases over lifetime

    private const float Duration  = 0.75f;
    private const float FallSpeed = 0.22f;
    private const float Size      = 0.095f;

    private static readonly Color ColBright = new Color(0.25f, 0.65f, 1.00f, 1.00f);
    private static readonly Color ColDim    = new Color(0.25f, 0.65f, 1.00f, 0.00f);

    /// <summary>
    /// Called immediately after AddComponent by EnemySlowIndicator.SpawnArrow().
    /// </summary>
    public void Init(Transform enemy, float xOffset, float heightOffset)
    {
        _enemy      = enemy;
        _xOffset    = xOffset;
        _fallOffset = heightOffset;
    }

    void Awake()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace   = true;
        _lr.loop            = false;
        _lr.positionCount   = 5;
        _lr.widthMultiplier = 0.020f;
        _lr.material        = new Material(Shader.Find("Sprites/Default"));
        _lr.sortingOrder    = 18;
        _lr.startColor      = ColBright;
        _lr.endColor        = ColBright;
    }

    void Update()
    {
        _timer      += Time.deltaTime;
        _fallOffset -= FallSpeed * Time.deltaTime;

        float t = Mathf.Clamp01(_timer / Duration);

        // Base position: track the enemy if alive, else stay put in world space
        Vector3 base3 = _enemy != null
            ? _enemy.position + new Vector3(_xOffset, _fallOffset, 0f)
            : transform.position;

        transform.position = base3;

        float half  = Size * 0.50f;
        float wing  = Size * 0.50f;
        float shaft = Size * 0.85f;

        _lr.SetPositions(new Vector3[]
        {
            base3 + new Vector3( 0f,   shaft, 0f),
            base3 + new Vector3( 0f,   0f,    0f),
            base3 + new Vector3(-half, wing,  0f),
            base3 + new Vector3( 0f,   0f,    0f),
            base3 + new Vector3( half, wing,  0f),
        });

        Color c = Color.Lerp(ColBright, ColDim, t);
        _lr.startColor = c;
        _lr.endColor   = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
