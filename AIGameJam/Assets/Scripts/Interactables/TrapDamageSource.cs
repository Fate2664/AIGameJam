using UnityEngine;

public interface IEnemyCellHazard
{
    float Damage { get; }
    float DamageCooldown { get; }
    float KnockbackHorizontalForce { get; }
    float KnockbackVerticalForce { get; }
    Transform SourceTransform { get; }
}

[DisallowMultipleComponent]
public class TrapDamageSource : MonoBehaviour, IEnemyCellHazard
{
    [SerializeField] [Min(0f)] private float damage = 10f;
    [SerializeField] [Min(0.05f)] private float damageCooldown = 0.75f;
    [SerializeField] [Min(0f)] private float knockbackHorizontalForce = 0.75f;
    [SerializeField] [Min(0f)] private float knockbackVerticalForce = 0.35f;

    public float Damage => damage;
    public float DamageCooldown => damageCooldown;
    public float KnockbackHorizontalForce => knockbackHorizontalForce;
    public float KnockbackVerticalForce => knockbackVerticalForce;
    public Transform SourceTransform => transform;
}
