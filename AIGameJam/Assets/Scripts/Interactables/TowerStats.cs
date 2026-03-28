using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TowerStats : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] [Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDepleted = false;

    [Header("Damage Feedback")]
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] [Min(0f)] private float damageFlashDuration = 0.2f;
    [SerializeField] [Min(0.01f)] private float damageFlashSpeed = 0.05f;

    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private SpriteDamageFlashEffect damageFlashEffect;
    private float currentHealth;

    public event Action<TowerStats, float> Damaged;
    public event Action<TowerStats> Depleted;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => Mathf.Max(1f, maxHealth);
    public bool IsDepleted => currentHealth <= 0f;

    private void Reset()
    {
        CacheSpriteRenderers();
    }

    private void Awake()
    {
        CacheSpriteRenderers();
        currentHealth = MaxHealth;
    }

    private void OnDisable()
    {
        damageFlashEffect?.Stop();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);

        if (!Application.isPlaying)
        {
            CacheSpriteRenderers();
        }
    }

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        damageFlashEffect?.Play(damageFlashColor, damageFlashDuration, damageFlashSpeed);
        Damaged?.Invoke(this, damage);

        if (currentHealth <= 0f)
        {
            HandleDepleted();
        }
    }

    public void RestoreFullHealth()
    {
        currentHealth = MaxHealth;
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        damageFlashEffect ??= new SpriteDamageFlashEffect(this);
        damageFlashEffect.CacheRenderers(spriteRenderers);
    }

    private void HandleDepleted()
    {
        Depleted?.Invoke(this);

        if (destroyOnDepleted)
        {
            Destroy(gameObject);
        }
    }
}
