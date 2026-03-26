using Pathfinding;
using UnityEngine;

[RequireComponent(typeof(Seeker), typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private Transform target = null;
    [SerializeField] private string targetObjectName = "Desert Tower";

    [Header("Movement")]
    [SerializeField] [Min(0f)] private float speed = 5f;
    [SerializeField] [Min(0f)] private float nextWaypointDistance = 1f;
    [SerializeField] [Min(0.05f)] private float pathRefreshInterval = 0.5f;
    [SerializeField] private EnemyMovementProfile fallbackMovement = EnemyMovementProfile.Default;

    private Path path;
    private int currentWaypoint;
    private Seeker seeker;
    private Rigidbody2D rb;
    private IEnemy enemy;
    private EnemyActor enemyActor;
    private GridManager gridManager;
    private float movementSeed;

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
        enemy = FindEnemy();
        enemyActor = GetComponent<EnemyActor>();
        gridManager = ResolveGridManager();
        movementSeed = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        InvokeRepeating(nameof(UpdatePath), 0f, pathRefreshInterval);
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

        if (!HasPath())
        {
            StopMovement();
            EvaluateCellHazard();
            return;
        }

        AdvanceWaypointIfReached();
        if (!HasPath())
        {
            StopMovement();
            EvaluateCellHazard();
            return;
        }

        MoveTowardsCurrentWaypoint();
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
        if (target == null)
        {
            target = ResolveTarget();
        }

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
            transform.localScale = Vector3.one;
        }
        else if (horizontalDirection <= -0.01f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
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
        return targetObject != null ? targetObject.transform : null;
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

    private static IEnemyCellHazard FindCellHazard(GameObject placedItem)
    {
        if (placedItem == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = placedItem.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyCellHazard hazard)
            {
                return hazard;
            }
        }

        return null;
    }

    private GridManager ResolveGridManager()
    {
        return gridManager != null ? gridManager : FindAnyObjectByType<GridManager>();
    }
}
