using Pathfinding;
using System;
using UnityEngine;

[RequireComponent(typeof(Seeker), typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private Transform target = null;
    [SerializeField] private string targetObjectName = "Desert Tower";
    [SerializeField] [Min(0f)] private float targetAttackPadding = 0.05f;
    [SerializeField] [Min(0.05f)] private float fallbackTargetDamageInterval = 0.75f;

    [Header("Movement")]
    [SerializeField] [Min(0f)] private float speed = 5f;
    [SerializeField] [Min(0f)] private float nextWaypointDistance = 1f;
    [SerializeField] [Min(0.05f)] private float pathRefreshInterval = 0.5f;
    [SerializeField] private EnemyMovementProfile fallbackMovement = EnemyMovementProfile.Default;

    [Header("Visuals")]
    [SerializeField] [Min(0f)] private float bobScaleReduction = 0.08f;
    [SerializeField] [Min(0f)] private float bobSpeed = 8f;
    [SerializeField] [Min(0f)] private float bobReturnSpeed = 6f;
    [SerializeField] [Min(0f)] private float bobMovementThreshold = 0.05f;

    [Header("Wall Attack")]
    [SerializeField] [Min(0f)] private float wallDetectionDistance = 0.2f;
    [SerializeField] private Vector2 wallDetectionSize = new(0.2f, 0.35f);
    [SerializeField] [Min(0.05f)] private float fallbackWallDamageInterval = 0.75f;
    [SerializeField] private LayerMask wallDetectionLayers = Physics2D.DefaultRaycastLayers;

    private Path path;
    private int currentWaypoint;
    private Seeker seeker;
    private Rigidbody2D rb;
    private IEnemy enemy;
    private EnemyActor enemyActor;
    private DefenseAreaController defenseArea;
    private GridManager gridManager;
    private Collider2D[] selfColliders = Array.Empty<Collider2D>();
    private readonly Collider2D[] wallDetectionHits = new Collider2D[8];
    private Vector2 lastMovementDirection = Vector2.right;
    private float lastWallDamageTime = float.NegativeInfinity;
    private float movementSeed;
    private float lastTargetDamageTime = float.NegativeInfinity;
    private Vector3 baseVisualScale = Vector3.one;
    private float facingDirection = 1f;
    private float currentVerticalScale = 1f;
    private IDamageable targetDamageable;
    private Transform targetDamageableTransform;
    private Collider2D[] targetColliders = Array.Empty<Collider2D>();

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
        enemy = FindEnemy();
        enemyActor = GetComponent<EnemyActor>();
        defenseArea = ResolveDefenseArea();
        gridManager = ResolveGridManager();
        selfColliders = GetComponentsInChildren<Collider2D>(true);
        baseVisualScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));
        facingDirection = transform.localScale.x < 0f ? -1f : 1f;
        currentVerticalScale = 1f;
        ApplyVisualScale();
        lastMovementDirection = facingDirection < 0f ? Vector2.left : Vector2.right;
        movementSeed = UnityEngine.Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        InvokeRepeating(nameof(UpdatePath), 0f, pathRefreshInterval);
    }

    private void Update()
    {
        UpdateVisualScale();
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(UpdatePath));
        StopMovement();
    }

    private void FixedUpdate()
    {
        if (enemyActor != null && enemyActor.IsInKnockback)
        {
            EvaluateCellHazard();
            return;
        }

        if (TryAttackTarget())
        {
            StopMovement();
            EvaluateCellHazard();
            return;
        }

        if (!HasPath())
        {
            StopMovement();
            EvaluateBlockingWall();
            EvaluateCellHazard();
            return;
        }

        AdvanceWaypointIfReached();
        if (!HasPath())
        {
            StopMovement();
            EvaluateBlockingWall();
            EvaluateCellHazard();
            return;
        }

        MoveTowardsCurrentWaypoint();
        EvaluateBlockingWall();
        EvaluateCellHazard();
    }

    private bool HasPath()
    {
        return path != null &&
               path.vectorPath != null &&
               currentWaypoint >= 0 &&
               currentWaypoint < path.vectorPath.Count;
    }

    private void AdvanceWaypointIfReached()
    {
        while (HasPath())
        {
            Vector2 waypoint = path.vectorPath[currentWaypoint];
            if (Vector2.Distance(rb.position, waypoint) > nextWaypointDistance)
            {
                break;
            }

            currentWaypoint++;
        }
    }

    private void MoveTowardsCurrentWaypoint()
    {
        Vector2 currentPosition = rb.position;
        Vector2 waypoint = path.vectorPath[currentWaypoint];
        Vector2 direction = ResolveMovementDirection(currentPosition, waypoint);

        if (direction.sqrMagnitude > Mathf.Epsilon)
        {
            lastMovementDirection = direction;
        }

        rb.linearVelocity = direction * ResolveMovementSpeed();
        UpdateFacing(direction.x);
    }

    private void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdatePath()
    {
        target = ResolveCurrentTarget();

        if (target == null || seeker == null || rb == null || !seeker.IsDone())
        {
            return;
        }

        seeker.StartPath(rb.position, target.position, OnPathComplete);
    }

    private void OnPathComplete(Path completedPath)
    {
        if (completedPath.error)
        {
            return;
        }

        path = completedPath;
        currentWaypoint = 0;
    }

    private void UpdateFacing(float horizontalDirection)
    {
        if (horizontalDirection >= 0.01f)
        {
            facingDirection = 1f;
        }
        else if (horizontalDirection <= -0.01f)
        {
            facingDirection = -1f;
        }
    }

    private float ResolveMovementSpeed()
    {
        return enemy != null ? enemy.MovementSpeed : speed;
    }

    private Vector2 ResolveMovementDirection(Vector2 currentPosition, Vector2 waypoint)
    {
        Vector2 pathDirection = waypoint - currentPosition;
        if (pathDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        pathDirection.Normalize();

        EnemyMovementProfile movement = ResolveMovementProfile();
        if (movement.LateralWobbleAmplitude <= 0f && movement.NoiseAmplitude <= 0f)
        {
            return pathDirection;
        }

        Vector2 lateralDirection = new Vector2(-pathDirection.y, pathDirection.x);
        float time = Time.fixedTime + movementSeed;
        float wobble = Mathf.Sin(time * movement.LateralWobbleFrequency) * movement.LateralWobbleAmplitude;
        float noise = (Mathf.PerlinNoise(movementSeed, Time.fixedTime * movement.NoiseFrequency) * 2f - 1f) * movement.NoiseAmplitude;

        Vector2 adjustedDirection = pathDirection + lateralDirection * (wobble + noise);
        if (adjustedDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return pathDirection;
        }

        return adjustedDirection.normalized;
    }

    private EnemyMovementProfile ResolveMovementProfile()
    {
        if (enemy != null && enemy.Definition != null)
        {
            return enemy.Definition.Movement;
        }

        return fallbackMovement;
    }

    private Transform ResolveTarget()
    {
        if (string.IsNullOrWhiteSpace(targetObjectName))
        {
            return null;
        }

        GameObject targetObject = GameObject.Find(targetObjectName);
        if (targetObject == null)
        {
            return null;
        }

        MonoBehaviour targetOwner = FindInterfaceInHierarchy<IDamageable>(targetObject.transform, out _);
        return targetOwner != null ? targetOwner.transform : targetObject.transform;
    }

    private Transform ResolveCurrentTarget()
    {
        Transform areaTarget = ResolveAreaTarget();
        if (areaTarget != null)
        {
            target = areaTarget;
            return target;
        }

        if (target != null)
        {
            return target;
        }

        target = ResolveTarget();
        return target;
    }

    private IEnemy FindEnemy()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemy foundEnemy)
            {
                return foundEnemy;
            }
        }

        return null;
    }

    private void EvaluateCellHazard()
    {
        if (rb == null || enemyActor == null || enemyActor.CurrentHealth <= 0f)
        {
            return;
        }

        if (gridManager == null)
        {
            gridManager = ResolveGridManager();
            if (gridManager == null)
            {
                return;
            }
        }

        if (!gridManager.TryGetPlacedItemAtWorldPosition(rb.position, out GameObject placedItem) || placedItem == null)
        {
            return;
        }

        IEnemyCellHazard hazard = FindCellHazard(placedItem);
        if (hazard == null)
        {
            return;
        }

        enemyActor.TryApplyHazard(hazard);
    }

    private void EvaluateBlockingWall()
    {
        if (enemyActor == null || enemyActor.CurrentHealth <= 0f || enemy == null || enemy.Damage <= 0f)
        {
            return;
        }

        float wallDamageInterval = ResolveWallDamageInterval();
        if (Time.time < lastWallDamageTime + wallDamageInterval)
        {
            return;
        }

        if (!TryGetWallInFront(out PlacedLoadoutItemActor wallActor))
        {
            return;
        }

        lastWallDamageTime = Time.time;
        wallActor.ApplyDamage(enemy.Damage);
    }

    private static IEnemyCellHazard FindCellHazard(GameObject placedItem)
    {
        return placedItem == null ? null : FindInterfaceInChildren<IEnemyCellHazard>(placedItem.transform, true, out _);
    }

    private bool TryGetWallInFront(out PlacedLoadoutItemActor wallActor)
    {
        wallActor = null;
        if (rb == null)
        {
            return false;
        }

        Vector2 detectionDirection = ResolveWallDetectionDirection();
        if (detectionDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        Vector2 center = rb.position + detectionDirection * wallDetectionDistance;
        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(wallDetectionLayers);
        contactFilter.useLayerMask = true;

        int hitCount = Physics2D.OverlapBox(center, wallDetectionSize, 0f, contactFilter, wallDetectionHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = wallDetectionHits[i];
            wallDetectionHits[i] = null;

            if (hit == null || hit.attachedRigidbody == rb || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            PlacedLoadoutItemActor candidate = hit.GetComponentInParent<PlacedLoadoutItemActor>();
            if (candidate == null || !candidate.IsWall)
            {
                continue;
            }

            wallActor = candidate;
            ClearWallDetectionHits(hitCount, i + 1);
            return true;
        }

        ClearWallDetectionHits(hitCount, 0);
        return false;
    }

    private GridManager ResolveGridManager()
    {
        if (gridManager != null)
        {
            return gridManager;
        }

        DefenseAreaController area = ResolveDefenseArea();
        if (area != null)
        {
            gridManager = area.GridManager;
            return gridManager;
        }

        return FindAnyObjectByType<GridManager>();
    }

    private Transform ResolveAreaTarget()
    {
        DefenseAreaController area = ResolveDefenseArea();
        return area != null ? area.TargetRoot : null;
    }

    private DefenseAreaController ResolveDefenseArea()
    {
        if (defenseArea != null)
        {
            return defenseArea;
        }

        defenseArea = DefenseAreaController.FindForTransform(transform);
        if (defenseArea != null)
        {
            return defenseArea;
        }

        defenseArea = DefenseAreaController.FindClosest(transform.position);
        return defenseArea;
    }

    private Vector2 ResolveWallDetectionDirection()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            return rb.linearVelocity.normalized;
        }

        if (lastMovementDirection.sqrMagnitude > 0.0001f)
        {
            return lastMovementDirection.normalized;
        }

        return facingDirection < 0f ? Vector2.left : Vector2.right;
    }

    private void UpdateVisualScale()
    {
        if (baseVisualScale.x <= Mathf.Epsilon)
        {
            return;
        }

        float targetVerticalScale = ResolveTargetVerticalScale();
        if (Mathf.Approximately(targetVerticalScale, currentVerticalScale))
        {
            ApplyVisualScale();
            return;
        }

        float interpolationSpeed = targetVerticalScale < currentVerticalScale ? bobSpeed : bobReturnSpeed;
        currentVerticalScale = Mathf.MoveTowards(currentVerticalScale, targetVerticalScale, interpolationSpeed * Time.deltaTime);
        ApplyVisualScale();
    }

    private float ResolveTargetVerticalScale()
    {
        if (rb == null || bobScaleReduction <= 0f || bobSpeed <= 0f)
        {
            return 1f;
        }

        if (enemyActor != null && enemyActor.IsInKnockback)
        {
            return 1f;
        }

        float movementThresholdSquared = bobMovementThreshold * bobMovementThreshold;
        if (rb.linearVelocity.sqrMagnitude <= movementThresholdSquared)
        {
            return 1f;
        }

        float bobWave = (Mathf.Sin((Time.time + movementSeed) * bobSpeed) + 1f) * 0.5f;
        return 1f - bobScaleReduction * bobWave;
    }

    private void ApplyVisualScale()
    {
        transform.localScale = new Vector3(
            baseVisualScale.x * facingDirection,
            baseVisualScale.y * currentVerticalScale,
            baseVisualScale.z);
    }

    private float ResolveWallDamageInterval()
    {
        return ResolveStructureDamageInterval(fallbackWallDamageInterval);
    }

    private float ResolveTargetDamageInterval()
    {
        return ResolveStructureDamageInterval(fallbackTargetDamageInterval);
    }

    private float ResolveStructureDamageInterval(float fallbackInterval)
    {
        if (enemy != null && enemy.Definition != null)
        {
            return Mathf.Max(0.05f, enemy.Definition.Stats.StructureDamageInterval);
        }

        return Mathf.Max(0.05f, fallbackInterval);
    }

    private void ClearWallDetectionHits(int hitCount, int clearedCount)
    {
        for (int i = clearedCount; i < hitCount; i++)
        {
            wallDetectionHits[i] = null;
        }
    }

    private bool TryAttackTarget()
    {
        if (enemyActor == null || enemyActor.CurrentHealth <= 0f || enemy == null || enemy.Damage <= 0f)
        {
            return false;
        }

        if (!TryResolveTargetDamageable(out IDamageable damageable))
        {
            return false;
        }

        if (!IsTargetInAttackRange())
        {
            return false;
        }

        if (Time.time < lastTargetDamageTime + ResolveTargetDamageInterval())
        {
            return true;
        }

        lastTargetDamageTime = Time.time;
        damageable.ApplyDamage(enemy.Damage);
        return true;
    }

    private bool TryResolveTargetDamageable(out IDamageable damageable)
    {
        damageable = null;
        Transform currentTarget = ResolveCurrentTarget();
        if (currentTarget == null)
        {
            ClearTargetDamageableCache();
            return false;
        }

        if (targetDamageable is MonoBehaviour cachedBehaviour &&
            cachedBehaviour != null &&
            cachedBehaviour.gameObject.activeInHierarchy)
        {
            damageable = targetDamageable;
            return true;
        }

        MonoBehaviour damageableBehaviour = FindInterfaceInHierarchy<IDamageable>(currentTarget, out damageable);
        if (damageableBehaviour == null || damageable == null)
        {
            ClearTargetDamageableCache();
            return false;
        }

        targetDamageable = damageable;
        targetDamageableTransform = damageableBehaviour.transform;
        targetColliders = targetDamageableTransform.GetComponentsInChildren<Collider2D>(true);
        return true;
    }

    private bool IsTargetInAttackRange()
    {
        for (int selfIndex = 0; selfIndex < selfColliders.Length; selfIndex++)
        {
            Collider2D selfCollider = selfColliders[selfIndex];
            if (!IsUsableCollider(selfCollider))
            {
                continue;
            }

            for (int targetIndex = 0; targetIndex < targetColliders.Length; targetIndex++)
            {
                Collider2D targetCollider = targetColliders[targetIndex];
                if (!IsUsableCollider(targetCollider))
                {
                    continue;
                }

                ColliderDistance2D distance = selfCollider.Distance(targetCollider);
                if (distance.isOverlapped || distance.distance <= targetAttackPadding)
                {
                    return true;
                }
            }
        }

        if (rb == null || targetDamageableTransform == null)
        {
            return false;
        }

        return Vector2.Distance(rb.position, targetDamageableTransform.position) <= targetAttackPadding;
    }

    private void ClearTargetDamageableCache()
    {
        targetDamageable = null;
        targetDamageableTransform = null;
        targetColliders = Array.Empty<Collider2D>();
    }

    private static bool IsUsableCollider(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy;
    }

    private static T FindInterfaceInChildren<T>(Transform root, bool includeRoot, out MonoBehaviour owner) where T : class
    {
        owner = null;
        if (root == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || (!includeRoot && behaviour.transform == root))
            {
                continue;
            }

            if (behaviour is T match)
            {
                owner = behaviour;
                return match;
            }
        }

        return null;
    }

    private static MonoBehaviour FindInterfaceInHierarchy<T>(Transform origin, out T match) where T : class
    {
        match = null;
        if (origin == null)
        {
            return null;
        }

        MonoBehaviour owner = FindInterfaceOnTransform(origin, out match);
        if (owner != null)
        {
            return owner;
        }

        Transform current = origin.parent;
        while (current != null)
        {
            owner = FindInterfaceOnTransform(current, out match);
            if (owner != null)
            {
                return owner;
            }

            current = current.parent;
        }

        match = FindInterfaceInChildren<T>(origin, false, out owner);
        return owner;
    }

    private static MonoBehaviour FindInterfaceOnTransform<T>(Transform transformToCheck, out T match) where T : class
    {
        match = null;
        if (transformToCheck == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = transformToCheck.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T typedMatch)
            {
                match = typedMatch;
                return behaviours[i];
            }
        }

        return null;
    }
}
