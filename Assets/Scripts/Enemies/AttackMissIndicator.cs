using UnityEngine;

/// <summary>
/// Spawns a brief, subtle "miss / immune" rebound on an enemy when an attack is
/// blocked or dodged.
///
/// The effect: enemy sprite nudges 0.1 u away from the attacker then springs back
/// over 0.18 s, plus a small white burst of 4 particles.
///
/// Call AttackMissIndicator.Spawn(enemyTransform, attackerPos) from EnemyInstance.
/// </summary>
public class AttackMissIndicator : MonoBehaviour
{
    private Vector3 _startPos;
    private Vector3 _nudgePos;
    private float   _timer;

    private const float Duration  = 0.18f;
    private const float NudgeDist = 0.1f;

    public static void Spawn(Transform enemy, Vector3 attackerPos)
    {
        // Nudge direction: away from attacker, in 2D
        Vector2 dir2 = (Vector2)(enemy.position - attackerPos);
        if (dir2.sqrMagnitude < 0.0001f) dir2 = Vector2.up;
        dir2.Normalize();

        var go = new GameObject("MissIndicator");
        go.transform.position = enemy.position;
        var mi = go.AddComponent<AttackMissIndicator>();
        mi._startPos = enemy.position;
        mi._nudgePos = enemy.position + (Vector3)(dir2 * NudgeDist);
        mi.transform.SetParent(enemy, true);

        SpawnDeflectParticles(enemy.position);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        // Ping-pong: out to nudge at t=0.5, back to start by t=1
        float ping = t < 0.5f
            ? Mathf.Lerp(0f, 1f, t / 0.5f)
            : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);

        transform.localPosition = Vector3.Lerp(Vector3.zero, _nudgePos - _startPos, ping);

        if (t >= 1f)
        {
            transform.localPosition = Vector3.zero;
            Destroy(gameObject);
        }
    }

    private static void SpawnDeflectParticles(Vector3 pos)
    {
        var go = new GameObject("MissDeflect_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 1f, 1f, 0.9f),
                                   new Color(0.85f, 0.85f, 1f, 0.7f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 6;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.02f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(Color.white, 0f), new(Color.white, 1f) },
            new GradientAlphaKey[] { new(0.9f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 20;

        ps.Play();
    }
}
