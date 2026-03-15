using System.Collections;
using UnityEngine;

/// <summary>
/// Briefly freezes the attached Animator for a fixed number of frames on hit.
/// This "hit stop" technique adds weight to impacts — the attacker and victim
/// feel like they have real mass.
///
/// Call TriggerStop() from damage-dealing scripts (e.g. EnemyInstance.ApplyRaw)
/// or attack scripts on the moment of contact.
///
/// The stop is always in realtime frames (independent of Time.timeScale) so it
/// still works during the evolve-cutscene pause.
/// </summary>
[RequireComponent(typeof(Animator))]
public class HitStop : MonoBehaviour
{
    [Tooltip("How many Update frames the animator is frozen per hit.")]
    [SerializeField] private int   stopFrames   = 3;
    [Tooltip("Whether a new TriggerStop call can interrupt an in-progress stop.")]
    [SerializeField] private bool  interruptible = true;

    private Animator  _animator;
    private Coroutine _routine;
    private float     _savedSpeed;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _savedSpeed = _animator.speed;
    }

    /// <summary>Freeze the animator for <see cref="stopFrames"/> frames.</summary>
    public void TriggerStop()
    {
        if (_routine != null)
        {
            if (!interruptible) return;
            StopCoroutine(_routine);
            _animator.speed = _savedSpeed; // restore before restarting
        }
        _routine = StartCoroutine(DoStop());
    }

    IEnumerator DoStop()
    {
        _savedSpeed     = _animator.speed;
        _animator.speed = 0f;

        for (int i = 0; i < stopFrames; i++)
            yield return null; // wait one frame per stop frame

        _animator.speed = _savedSpeed;
        _routine = null;
    }
}
