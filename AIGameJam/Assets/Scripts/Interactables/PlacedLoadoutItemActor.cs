using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlacedLoadoutItemActor : MonoBehaviour, IDamageable, IGridPlaceable
{
    [SerializeField] private LoadoutItemDefinition definition = null;
    [Header("Damage Feedback")]
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] [Min(0f)] private float damageFlashDuration = 0.2f;
    [SerializeField] [Min(0.01f)] private float damageFlashSpeed = 0.05f;

    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private SpriteDamageFlashEffect damageFlashEffect;
    private GridManager gridManager;
    private CellVisuals cell;
    private float currentHealth;

    public LoadoutItemDefinition Definition => definition;
    public bool IsWall => definition != null && definition.ItemType == LoadoutItemType.Wall;
    public bool UsesHealth => definition != null && definition.UsesHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => definition != null ? definition.MaxHealth : 0f;

    private void Reset()
    {
        CacheSpriteRenderers();
    }

    private void Awake()
    {
        CacheSpriteRenderers();
        ApplyDefinition(definition, true);
    }

    private void OnDisable()
    {
        StopDamageFlash();
    }

    public void Initialize(LoadoutItemDefinition loadoutItemDefinition)
    {
        CacheSpriteRenderers();
        ApplyDefinition(loadoutItemDefinition, true);
    }

    public void ApplyDamage(float damage)
    {
        if (!UsesHealth || damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        PlayDamageFlash();
        if (currentHealth <= 0f)
        {
            HandleDestroyed();
        }
    }

    public void OnPlaced(GridManager ownerGridManager, CellVisuals placedCell)
    {
        gridManager = ownerGridManager;
        cell = placedCell;
    }

    public void OnRemoved(GridManager ownerGridManager, CellVisuals removedCell)
    {
        if (gridManager == ownerGridManager)
        {
            gridManager = null;
        }

        if (cell == removedCell)
        {
            cell = null;
        }

        StopDamageFlash();
    }

    private void ApplyDefinition(LoadoutItemDefinition loadoutItemDefinition, bool resetHealth)
    {
        definition = loadoutItemDefinition;
        if (resetHealth)
        {
            currentHealth = MaxHealth;
        }

        if (UsesHealth)
        {
            EnsureWallColliders();
        }
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        damageFlashEffect ??= new SpriteDamageFlashEffect(this);
        damageFlashEffect.CacheRenderers(spriteRenderers);
    }

    private void EnsureWallColliders()
    {
        if (GetComponentsInChildren<Collider2D>(true).Length > 0)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || renderer.sprite == null || renderer.GetComponent<Collider2D>() != null)
            {
                continue;
            }

            BoxCollider2D collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.offset = renderer.sprite.bounds.center;
            collider.size = renderer.sprite.bounds.size;
        }
    }

    private void HandleDestroyed()
    {
        if (gridManager != null && cell != null)
        {
            gridManager.TryRemovePlacedItem(cell);
            return;
        }

        Destroy(gameObject);
    }

    private void PlayDamageFlash()
    {
        damageFlashEffect?.Play(damageFlashColor, damageFlashDuration, damageFlashSpeed);
    }

    private void StopDamageFlash()
    {
        damageFlashEffect?.Stop();
    }
}
