using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyActor : MonoBehaviour, IEnemy, IDamageable
{
    private static readonly List<Collider2D> RegisteredColliders = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegisteredColliders()
    {
        RegisteredColliders.Clear();
    }

    [SerializeField] private EnemyDefinition definition = null;
    [SerializeField] private SpriteRenderer spriteRenderer = null;
    [SerializeField] private bool destroyOnDeath = true;
    [Header("Damage Feedback")]
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] [Min(0f)] private float damageFlashDuration = 0.2f;
    [SerializeField] [Min(0.01f)] private float damageFlashSpeed = 0.05f;
    [SerializeField] [Min(0.01f)] private float knockbackDuration = 0.3f;

    private readonly Dictionary<int, float> lastHazardDamageTimes = new();
    private float currentHealth;
    private Collider2D[] colliders = Array.Empty<Collider2D>();
    private Coroutine damageFlashCoroutine;
    private Rigidbody2D rb;
    private Color defaultSpriteColor = Color.white;
    private float knockbackEndTime;

    public event Action<EnemyActor> Died;

    public IEnemyDefinition Definition => definition;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => definition != null ? definition.Stats.MaxHealth : 0f;
    public float Damage => definition != null ? definition.Stats.Damage : 0f;
    public float MovementSpeed => definition != null ? definition.Stats.MovementSpeed : 0f;
    public bool IsInKnockback => Time.time < knockbackEndTime;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer != null)
        {
            defaultSpriteColor = spriteRenderer.color;
        }

        RefreshColliders();
        ApplyDefinition(definition, true);
    }

    private void OnEnable()
    {
        RegisterCollisionsWithOtherEnemies();
    }

    private void OnDisable()
    {
        StopDamageFlash();
        UnregisterCollisionsWithOtherEnemies();
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (!Application.isPlaying)
        {
            ApplyVisuals();
        }
    }

    public void Initialize(EnemyDefinition enemyDefinition)
    {
        ApplyDefinition(enemyDefinition, true);
    }

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        PlayDamageFlash();
        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    public bool TryApplyHazard(IEnemyCellHazard hazard)
    {
        if (hazard == null || hazard.Damage <= 0f || currentHealth <= 0f)
        {
            return false;
        }

        Transform sourceTransform = hazard.SourceTransform;
        int hazardId = sourceTransform != null ? sourceTransform.GetInstanceID() : 0;
        float damageCooldown = Mathf.Max(0f, hazard.DamageCooldown);

        if (hazardId != 0 &&
            lastHazardDamageTimes.TryGetValue(hazardId, out float lastDamageTime) &&
            Time.time < lastDamageTime + damageCooldown)
        {
            return false;
        }

        if (hazardId != 0)
        {
            lastHazardDamageTimes[hazardId] = Time.time;
        }

        ApplyDamage(hazard.Damage);
        if (currentHealth <= 0f)
        {
            return true;
        }

        ApplyKnockback(sourceTransform, hazard.KnockbackHorizontalForce, hazard.KnockbackVerticalForce);
        return true;
    }

    private void ApplyDefinition(EnemyDefinition enemyDefinition, bool resetHealth)
    {
        definition = enemyDefinition;
        if (resetHealth)
        {
            currentHealth = MaxHealth;
        }

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (spriteRenderer != null && definition != null && definition.Sprite != null)
        {
            spriteRenderer.sprite = definition.Sprite;
        }
    }

    private void HandleDeath()
    {
        Died?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }

    private void RefreshColliders()
    {
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void ApplyKnockback(Transform source, float horizontalForce, float verticalForce)
    {
        if (rb == null || source == null || (horizontalForce <= 0f && verticalForce <= 0f))
        {
            return;
        }

        float direction = transform.position.x > source.position.x ? 1f : -1f;
        if (Mathf.Approximately(transform.position.x, source.position.x))
        {
            direction = transform.localScale.x >= 0f ? 1f : -1f;
        }

        knockbackEndTime = Time.time + knockbackDuration;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(direction * horizontalForce, verticalForce), ForceMode2D.Impulse);
    }

    private void PlayDamageFlash()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
        }

        damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private void StopDamageFlash()
    {
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultSpriteColor;
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, damageFlashDuration);
        float flashSpeed = Mathf.Max(0.01f, damageFlashSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(flashSpeed);
            spriteRenderer.color = defaultSpriteColor;
            yield return new WaitForSeconds(flashSpeed);
            elapsed += flashSpeed * 2f;
        }

        spriteRenderer.color = defaultSpriteColor;
        damageFlashCoroutine = null;
    }

    private void RegisterCollisionsWithOtherEnemies()
    {
        if (colliders == null || colliders.Length == 0)
        {
            RefreshColliders();
        }

        CleanupRegisteredColliders();

        for (int firstIndex = 0; firstIndex < colliders.Length; firstIndex++)
        {
            Collider2D firstCollider = colliders[firstIndex];
            if (!IsRegisterableCollider(firstCollider))
            {
                continue;
            }

            for (int secondIndex = firstIndex + 1; secondIndex < colliders.Length; secondIndex++)
            {
                Collider2D secondCollider = colliders[secondIndex];
                if (IsRegisterableCollider(secondCollider))
                {
                    Physics2D.IgnoreCollision(firstCollider, secondCollider, true);
                }
            }
        }

        for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
        {
            Collider2D currentCollider = colliders[colliderIndex];
            if (!IsRegisterableCollider(currentCollider))
            {
                continue;
            }

            for (int registeredIndex = 0; registeredIndex < RegisteredColliders.Count; registeredIndex++)
            {
                Collider2D registeredCollider = RegisteredColliders[registeredIndex];
                if (!IsRegisterableCollider(registeredCollider) || registeredCollider == currentCollider)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(currentCollider, registeredCollider, true);
            }

            if (!RegisteredColliders.Contains(currentCollider))
            {
                RegisteredColliders.Add(currentCollider);
            }
        }
    }

    private void UnregisterCollisionsWithOtherEnemies()
    {
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
        {
            RegisteredColliders.Remove(colliders[colliderIndex]);
        }
    }

    private static void CleanupRegisteredColliders()
    {
        for (int i = RegisteredColliders.Count - 1; i >= 0; i--)
        {
            if (!IsRegisterableCollider(RegisteredColliders[i]))
            {
                RegisteredColliders.RemoveAt(i);
            }
        }
    }

    private static bool IsRegisterableCollider(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy &&
               !collider.isTrigger;
    }
}
