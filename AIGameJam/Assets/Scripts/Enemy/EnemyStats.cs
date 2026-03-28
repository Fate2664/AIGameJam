using System;
using UnityEngine;

[Serializable]
public struct EnemyStats
{
    [Min(1f)] public float MaxHealth;
    [Min(0f)] public float Damage;
    [Min(0f)] public float MovementSpeed;
    [Min(0.05f)] public float StructureDamageInterval;
    [Min(0)] public int CurrencyReward;

    public EnemyStats(float maxHealth, float damage, float movementSpeed, float structureDamageInterval = 0.75f, int currencyReward = 0)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        Damage = Mathf.Max(0f, damage);
        MovementSpeed = Mathf.Max(0f, movementSpeed);
        StructureDamageInterval = Mathf.Max(0.05f, structureDamageInterval);
        CurrencyReward = Mathf.Max(0, currencyReward);
    }
}
