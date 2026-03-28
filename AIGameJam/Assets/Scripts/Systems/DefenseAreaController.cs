using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DefenseAreaController : MonoBehaviour
{
    private static readonly List<DefenseAreaController> RegisteredAreas = new();

    [SerializeField] private TowerStats towerStats = null;
    [SerializeField] private Transform targetRoot = null;
    [SerializeField] private Transform focusPoint = null;
    [SerializeField] private GridManager gridManager = null;

    public event System.Action<DefenseAreaController, TowerStats, float> AreaDamaged;

    public TowerStats TowerStats => ResolveTowerStatsReference();
    public Transform TargetRoot => targetRoot != null ? targetRoot : transform;
    public Transform FocusPoint => focusPoint != null ? focusPoint : transform;
    public GridManager GridManager => ResolveGridManagerReference();

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        SubscribeToTowerStats();

        if (!RegisteredAreas.Contains(this))
        {
            RegisteredAreas.Add(this);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromTowerStats();
        RegisteredAreas.Remove(this);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoAssignReferences();
        }
    }

    public static DefenseAreaController FindForTransform(Transform origin)
    {
        return origin != null ? origin.GetComponentInParent<DefenseAreaController>() : null;
    }

    public static DefenseAreaController FindClosest(Vector3 position)
    {
        CleanupRegisteredAreas();

        DefenseAreaController closestArea = null;
        float closestDistanceSquared = float.MaxValue;

        for (int i = 0; i < RegisteredAreas.Count; i++)
        {
            DefenseAreaController candidate = RegisteredAreas[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 candidatePosition = candidate.TargetRoot != null ? candidate.TargetRoot.position : candidate.transform.position;
            float distanceSquared = (candidatePosition - position).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestArea = candidate;
        }

        return closestArea;
    }

    private void AutoAssignReferences()
    {
        if (towerStats == null)
        {
            towerStats = GetComponentInChildren<TowerStats>(true);
        }

        if (targetRoot == null)
        {
            if (towerStats != null)
            {
                targetRoot = towerStats.transform;
            }
            else
            {
                targetRoot = FindTowerTransformByName();
            }
        }

        if (gridManager == null)
        {
            gridManager = GetComponentInChildren<GridManager>(true);
        }
    }

    private GridManager ResolveGridManagerReference()
    {
        if (gridManager == null)
        {
            gridManager = GetComponentInChildren<GridManager>(true);
        }

        return gridManager;
    }

    private TowerStats ResolveTowerStatsReference()
    {
        if (towerStats == null)
        {
            towerStats = GetComponentInChildren<TowerStats>(true);
        }

        return towerStats;
    }

    private Transform FindTowerTransformByName()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform childTransform = childTransforms[i];
            if (childTransform == null || childTransform == transform)
            {
                continue;
            }

            if (childTransform.name.IndexOf("Tower", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return childTransform;
            }
        }

        return null;
    }

    private static void CleanupRegisteredAreas()
    {
        for (int i = RegisteredAreas.Count - 1; i >= 0; i--)
        {
            if (RegisteredAreas[i] == null)
            {
                RegisteredAreas.RemoveAt(i);
            }
        }
    }

    private void SubscribeToTowerStats()
    {
        TowerStats resolvedTowerStats = ResolveTowerStatsReference();
        if (resolvedTowerStats != null)
        {
            resolvedTowerStats.Damaged -= HandleTowerDamaged;
            resolvedTowerStats.Damaged += HandleTowerDamaged;
        }
    }

    private void UnsubscribeFromTowerStats()
    {
        if (towerStats != null)
        {
            towerStats.Damaged -= HandleTowerDamaged;
        }
    }

    private void HandleTowerDamaged(TowerStats damagedTower, float damageAmount)
    {
        AreaDamaged?.Invoke(this, damagedTower, damageAmount);
    }
}
