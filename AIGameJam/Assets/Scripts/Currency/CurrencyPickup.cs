using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CurrencyPickup : MonoBehaviour
{
    [SerializeField] [Min(1)] private int currencyAmount = 1;
    [SerializeField] private Collider2D clickCollider = null;

    private bool isCollected;
    private Camera cachedCamera;
    private LoadoutMenu cachedLoadoutMenu;

    public bool IsCollected => isCollected;

    private void Reset()
    {
        EnsureCollider();
    }

    private void Awake()
    {
        EnsureCollider();
    }

    private void Update()
    {
        if (isCollected || !TryGetPointerDownWorldPosition(out Vector2 worldPosition))
        {
            return;
        }

        if (clickCollider == null)
        {
            EnsureCollider();
        }

        if (clickCollider != null && clickCollider.OverlapPoint(worldPosition))
        {
            Collect();
        }
    }

    public void Initialize(int amount)
    {
        currencyAmount = Mathf.Max(1, amount);
        EnsureCollider();
    }

    public void SetAmount(int amount)
    {
        Initialize(amount);
    }

    public static int CollectAllInScene()
    {
        CurrencyPickup[] pickups = FindObjectsByType<CurrencyPickup>(FindObjectsSortMode.None);
        int collectedAmount = 0;

        for (int i = 0; i < pickups.Length; i++)
        {
            CurrencyPickup pickup = pickups[i];
            if (pickup == null || pickup.isCollected)
            {
                continue;
            }

            collectedAmount += Mathf.Max(1, pickup.currencyAmount);
            pickup.Collect();
        }

        return collectedAmount;
    }

    public void Collect()
    {
        if (isCollected)
        {
            return;
        }

        isCollected = true;
        ResolveLoadoutMenu()?.AddBudget(currencyAmount);
        Destroy(gameObject);
    }

    private void EnsureCollider()
    {
        if (clickCollider == null)
        {
            clickCollider = GetComponent<Collider2D>();
        }

        if (clickCollider == null)
        {
            CircleCollider2D generatedCollider = gameObject.AddComponent<CircleCollider2D>();
            generatedCollider.radius = ResolveColliderRadius();
            clickCollider = generatedCollider;
        }

        clickCollider.isTrigger = true;
    }

    private float ResolveColliderRadius()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return 0.25f;
        }

        Vector3 localExtents = transform.InverseTransformVector(spriteRenderer.bounds.extents);
        float maxExtent = Mathf.Max(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y));
        return Mathf.Max(0.1f, maxExtent);
    }

    private bool TryGetPointerDownWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = default;
        Camera targetCamera = ResolveCamera();
        if (targetCamera == null)
        {
            return false;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 screenPosition = Mouse.current.position.ReadValue();
            worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector3 screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
            return true;
        }

        return false;
    }

    private Camera ResolveCamera()
    {
        if (cachedCamera != null)
        {
            return cachedCamera;
        }

        cachedCamera = Camera.main;
        if (cachedCamera == null)
        {
            cachedCamera = FindAnyObjectByType<Camera>();
        }

        return cachedCamera;
    }

    private LoadoutMenu ResolveLoadoutMenu()
    {
        if (cachedLoadoutMenu != null)
        {
            return cachedLoadoutMenu;
        }

        cachedLoadoutMenu = LoadoutMenu.Instance;
        if (cachedLoadoutMenu == null)
        {
            cachedLoadoutMenu = FindAnyObjectByType<LoadoutMenu>();
        }

        return cachedLoadoutMenu;
    }
}
