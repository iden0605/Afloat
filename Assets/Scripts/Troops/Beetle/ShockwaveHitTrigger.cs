using UnityEngine;

/// <summary>
/// Placed on the BeetleShockwaveCollider GameObject by BeetleGroundPoundAttack at runtime.
/// Forwards 2D trigger events to the parent attack component.
///
/// OnTriggerEnter2D fires when the growing shockwave circle first reaches an enemy.
/// OnTriggerStay2D  is a safety net in case Enter is missed for an enemy that was
/// already partially inside when the collider activated.
/// </summary>
public class ShockwaveHitTrigger : MonoBehaviour
{
    private BeetleGroundPoundAttack _attack;

    public void Init(BeetleGroundPoundAttack attack) => _attack = attack;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<EnemyMovement>(out var enemy))
            _attack.OnShockwaveHit(enemy);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent<EnemyMovement>(out var enemy))
            _attack.OnShockwaveHit(enemy);
    }
}
