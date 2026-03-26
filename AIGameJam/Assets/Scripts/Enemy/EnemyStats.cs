using System;
using UnityEngine;

[Serializable]
public struct EnemyStats
{
    [Min(1f)] public float MaxHealth;
    [Min(0f)] public float Damage;
    [Min(0f)] public float MovementSpeed;

    public EnemyStats(float maxHealth, float damage, float movementSpeed)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        Damage = Mathf.Max(0f, damage);
        MovementSpeed = Mathf.Max(0f, movementSpeed);
    }
}
