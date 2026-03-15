using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural tongue attack for the Frog troop.
/// Draws the tongue with a LineRenderer — no sprites required.
/// Reads CurrentTarget from TroopBehavior and fires on the interval
/// defined by TroopInstance.CurrentAttackSpeed.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class FrogTongueAttack : MonoBehaviour
{
    [Header("Tongue Visuals")]
    [SerializeField] private Color  tongueColor     = new Color(0.88f, 0.27f, 0.36f, 1f);
    [SerializeField] private float  tongueBaseWidth = 0.13f;
    [SerializeField] private float  tongueTipWidth  = 0.045f;
    [SerializeField] private float  mouthOffset     = 0.30f;   // local +Y offset from troop centre
    [SerializeField] private int    tongueSegments  = 16;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 5;

    [Header("Attack Timing")]
    [SerializeField] private float extendDuration  = 0.13f;
    [SerializeField] private float retractDuration = 0.09f;

    // ── Internal state ───────────────────────────────────────

    private enum Phase { Idle, Extending, Retracting }

    private TroopBehavior    _behavior;
    private TroopInstance    _instance;
    private LineRenderer     _tongue;
    private Transform        _tipTransform;

    private Phase         _phase       = Phase.Idle;
    private float         _phaseTimer  = 0f;
    private float         _cooldown    = 0f;
    private EnemyMovement _lockedTarget;
    private Vector3       _lockedTipPos;

    // Tracks which enemies were hit on this strike so each is only registered once
    private readonly HashSet<EnemyMovement> _hitThisStrike = new();

    // ── Awake ────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        BuildTongue();
        BuildTipCollider();
    }

    void BuildTongue()
    {
        var go = new GameObject("Tongue");
        go.transform.SetParent(transform, false);

        _tongue = go.AddComponent<LineRenderer>();
        _tongue.useWorldSpace     = true;
        _tongue.positionCount     = tongueSegments + 1;
        _tongue.loop              = false;
        _tongue.numCapVertices    = 6;
        _tongue.numCornerVertices = 4;
        _tongue.widthCurve        = MakeWidthCurve();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = tongueColor;
        _tongue.material         = mat;
        _tongue.sortingLayerName = sortingLayerName;
        _tongue.sortingOrder     = sortingOrder;
        _tongue.enabled          = false;
    }

    AnimationCurve MakeWidthCurve()
    {
        // Thick at the mouth (index 0), slight mid-bulge, tapers to a rounded tip
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0.00f, tongueBaseWidth,        0f,  -0.05f));
        c.AddKey(new Keyframe(0.30f, tongueBaseWidth * 0.85f, 0f,   0f));
        c.AddKey(new Keyframe(0.75f, tongueBaseWidth * 0.60f, 0f,   0f));
        c.AddKey(new Keyframe(1.00f, tongueTipWidth,         0f,   0f));
        return c;
    }

    void BuildTipCollider()
    {
        var go = new GameObject("TongueTip");
        go.transform.SetParent(transform, false);
        _tipTransform = go.transform;

        // Kinematic Rigidbody2D is required for OnTrigger callbacks to fire
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.12f;
        col.isTrigger = true;

        var trigger = go.AddComponent<TongueTipTrigger>();
        trigger.Init(this);

        go.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        if (_phase == Phase.Idle)
        {
            if (_cooldown <= 0f && _behavior.CurrentTarget != null)
                BeginExtend();
            return;
        }

        _phaseTimer += Time.deltaTime;

        if (_phase == Phase.Extending)
        {
            // Track the live enemy while extending so the tongue leads into them
            if (_lockedTarget != null)
                _lockedTipPos = _lockedTarget.transform.position;

            float t = Mathf.Clamp01(_phaseTimer / extendDuration);
            DrawTongue(EaseOutQuart(t));

            if (t >= 1f) BeginRetract();
        }
        else // Retracting — tip position is fixed at where the enemy was hit
        {
            float t = Mathf.Clamp01(_phaseTimer / retractDuration);
            DrawTongue(EaseInQuart(1f - t));

            if (t >= 1f) EndAttack();
        }
    }

    void BeginExtend()
    {
        _lockedTarget  = _behavior.CurrentTarget;
        _lockedTipPos  = _lockedTarget.transform.position;
        _phase         = Phase.Extending;
        _phaseTimer    = 0f;
        _hitThisStrike.Clear();
        _tongue.enabled = true;
        _tipTransform.gameObject.SetActive(true);
    }

    void BeginRetract()
    {
        _phase      = Phase.Retracting;
        _phaseTimer = 0f;
        // Deactivate tip so no more hits register during retract
        _tipTransform.gameObject.SetActive(false);
    }

    void EndAttack()
    {
        _phase          = Phase.Idle;
        _tongue.enabled = false;
        _cooldown       = _instance.CurrentAttackInterval;
    }

    // ── Drawing ───────────────────────────────────────────────

    void DrawTongue(float t)
    {
        // origin is the frog's mouth: mouthOffset units along local +Y (which TroopBehavior
        // keeps pointed toward the current target by rotating the whole transform)
        Vector3 origin = transform.TransformPoint(Vector3.up * mouthOffset);
        Vector3 tip    = Vector3.Lerp(origin, _lockedTipPos, t);

        _tipTransform.position = tip;

        Vector3 axis = tip - origin;
        float   len  = axis.magnitude;
        // Perpendicular in 2D for the mid-wiggle effect
        Vector3 perp = len > 0.001f
            ? new Vector3(-axis.y, axis.x, 0f).normalized
            : Vector3.right;

        for (int i = 0; i <= tongueSegments; i++)
        {
            float   s   = (float)i / tongueSegments;
            Vector3 pos = Vector3.Lerp(origin, tip, s);

            // Subtle sine-arc that fades out as tongue fully extends —
            // gives a fleshy, organic look during launch
            float wiggle = Mathf.Sin(s * Mathf.PI) * 0.055f * (1f - t);
            pos += perp * wiggle;

            _tongue.SetPosition(i, pos);
        }
    }

    // ── Hit registration (called by TongueTipTrigger) ─────────

    /// <summary>
    /// Called when the tongue tip's trigger collider overlaps an enemy.
    /// Each enemy is registered at most once per strike.
    /// Damage application is left for when the health system is implemented.
    /// </summary>
    public void OnTipHit(EnemyMovement enemy)
    {
        if (_phase != Phase.Extending) return;
        if (!_hitThisStrike.Add(enemy)) return; // already hit this strike

        enemy.TakeDamage(_instance.CurrentAttack, AttackType.Ranged);
    }

    // ── Easing functions ─────────────────────────────────────

    // Fast snap-out: slow deceleration as tongue reaches enemy
    static float EaseOutQuart(float t)
    {
        float f = 1f - t;
        return 1f - f * f * f * f;
    }

    // Accelerates back in — tongue snaps home quickly at the end
    static float EaseInQuart(float t) => t * t * t * t;
}
