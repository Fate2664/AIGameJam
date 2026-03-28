using System;
using System.Collections.Generic;
using System.Linq;
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
    [Header("Currency Drop")]
    [SerializeField] private GameObject currencyPrefab = null;
    [SerializeField] [Range(0f, 1f)] private float currencyDropChance = 0.5f;
    [SerializeField] [Min(0)] private int currencyAmount = 0;
    [SerializeField] private Vector3 currencySpawnOffset = new(0f, 0.15f, 0f);
    [Header("Damage Feedback")]
    [SerializeField] private ParticleSystem[] damageParticleSystems = Array.Empty<ParticleSystem>();
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] [Min(0f)] private float damageFlashDuration = 0.2f;
    [SerializeField] [Min(0.01f)] private float damageFlashSpeed = 0.05f;
    [SerializeField] [Min(0.01f)] private float knockbackDuration = 0.3f;

    private readonly Dictionary<int, float> lastHazardDamageTimes = new();
    private float currentHealth;
    private Collider2D[] colliders = Array.Empty<Collider2D>();
    private SpriteDamageFlashEffect damageFlashEffect;
    private Rigidbody2D rb;
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
        CacheDamageFlashRenderers();
        CacheDamageParticleSystems();
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void Awake()
    {
        CacheDamageFlashRenderers();
        CacheDamageParticleSystems();
        rb = GetComponent<Rigidbody2D>();
        RefreshColliders();
        ApplyDefinition(definition, true);
        StopDamageParticles(true);
    }

    private void OnEnable()
    {
        RegisterCollisionsWithOtherEnemies();
    }

    private void OnDisable()
    {
        StopDamageFlash();
        StopDamageParticles(true);
        UnregisterCollisionsWithOtherEnemies();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheDamageFlashRenderers();
            CacheDamageParticleSystems();
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
        PlayDamageParticles();
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
        TryDropCurrency();

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }

    private void RefreshColliders()
    {
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void TryDropCurrency()
    {
        if (currencyPrefab == null || currencyDropChance <= 0f)
        {
            return;
        }

        if (UnityEngine.Random.value > currencyDropChance)
        {
            return;
        }

        int droppedCurrencyAmount = ResolveCurrencyAmount();
        if (droppedCurrencyAmount <= 0)
        {
            return;
        }

        Transform parent = transform.parent;
        GameObject currencyInstance = parent != null
            ? Instantiate(currencyPrefab, transform.position + currencySpawnOffset, Quaternion.identity, parent)
            : Instantiate(currencyPrefab, transform.position + currencySpawnOffset, Quaternion.identity);
        CurrencyPickup pickup = currencyInstance.GetComponent<CurrencyPickup>();
        if (pickup == null)
        {
            pickup = currencyInstance.AddComponent<CurrencyPickup>();
        }

        pickup.Initialize(droppedCurrencyAmount);
    }

    private int ResolveCurrencyAmount()
    {
        if (currencyAmount > 0)
        {
            return currencyAmount;
        }

        if (definition != null && definition.Stats.CurrencyReward > 0)
        {
            return definition.Stats.CurrencyReward;
        }

        EnemyType enemyType = definition != null ? definition.EnemyType : EnemyType.Base;
        return ResolveDefaultCurrencyAmount(enemyType);
    }

    private static int ResolveDefaultCurrencyAmount(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Tank:
                return 2;
            case EnemyType.Boss:
                return 5;
            default:
                return 1;
        }
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
        damageFlashEffect?.Play(damageFlashColor, damageFlashDuration, damageFlashSpeed);
    }

    private void PlayDamageParticles()
    {
        if (damageParticleSystems == null || damageParticleSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < damageParticleSystems.Length; i++)
        {
            ParticleSystem damageParticleSystem = damageParticleSystems[i];
            if (damageParticleSystem == null)
            {
                continue;
            }

            GameObject particleSystemObject = damageParticleSystem.gameObject;
            if (!particleSystemObject.activeSelf)
            {
                particleSystemObject.SetActive(true);
            }

            damageParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            damageParticleSystem.Clear(true);
            damageParticleSystem.Play();
        }
    }

    private void StopDamageFlash()
    {
        damageFlashEffect?.Stop();
    }

    private void StopDamageParticles(bool clear)
    {
        if (damageParticleSystems == null || damageParticleSystems.Length == 0)
        {
            return;
        }

        ParticleSystemStopBehavior stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < damageParticleSystems.Length; i++)
        {
            ParticleSystem damageParticleSystem = damageParticleSystems[i];
            if (damageParticleSystem == null)
            {
                continue;
            }

            GameObject particleSystemObject = damageParticleSystem.gameObject;
            if (!particleSystemObject.activeSelf)
            {
                continue;
            }

            damageParticleSystem.Stop(true, stopBehavior);
            if (clear)
            {
                damageParticleSystem.Clear(true);
                particleSystemObject.SetActive(false);
            }
        }
    }

    private void CacheDamageFlashRenderers()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        SpriteRenderer[] flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (flashRenderers.Length == 0 && spriteRenderer != null)
        {
            flashRenderers = new[] { spriteRenderer };
        }

        damageFlashEffect ??= new SpriteDamageFlashEffect(this);
        damageFlashEffect.CacheRenderers(flashRenderers);
    }

    private void CacheDamageParticleSystems()
    {
        damageParticleSystems = damageParticleSystems
            .Where(particleSystem => particleSystem != null)
            .Distinct()
            .ToArray();

        ConfigureDamageParticleSystems(damageParticleSystems);

        if (damageParticleSystems.Length > 0)
        {
            return;
        }

        ParticleSystem[] discoveredParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        if (discoveredParticleSystems == null || discoveredParticleSystems.Length == 0)
        {
            return;
        }

        List<ParticleSystem> namedDamageParticleSystems = new();
        List<ParticleSystem> fallbackParticleSystems = new();

        for (int i = 0; i < discoveredParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = discoveredParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            if (IsDamageParticleSystem(particleSystem.transform))
            {
                namedDamageParticleSystems.Add(particleSystem);
                continue;
            }

            if (!particleSystem.main.loop)
            {
                fallbackParticleSystems.Add(particleSystem);
            }
        }

        damageParticleSystems = (namedDamageParticleSystems.Count > 0
                ? namedDamageParticleSystems
                : fallbackParticleSystems)
            .Distinct()
            .ToArray();

        ConfigureDamageParticleSystems(damageParticleSystems);
    }

    private bool IsDamageParticleSystem(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (current == transform)
            {
                break;
            }

            if (current.name.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                current.name.IndexOf("hit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void ConfigureDamageParticleSystems(IReadOnlyList<ParticleSystem> particleSystems)
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = false;
        }
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
