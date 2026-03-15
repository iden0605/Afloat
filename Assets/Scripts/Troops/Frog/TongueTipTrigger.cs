using UnityEngine;

/// <summary>
/// Placed on the TongueTip child GameObject by FrogTongueAttack at runtime.
/// Forwards 2D trigger events to the parent attack component.
/// </summary>
public class TongueTipTrigger : MonoBehaviour
{
    private FrogTongueAttack _attack;

    public void Init(FrogTongueAttack attack) => _attack = attack;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<EnemyMovement>(out var enemy))
            _attack.OnTipHit(enemy);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent<EnemyMovement>(out var enemy))
            _attack.OnTipHit(enemy);
    }
}
