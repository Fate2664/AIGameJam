using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaveController : MonoBehaviour
{
    [Serializable]
    public class EnemySpawnDefinition
    {
        public EnemyDefinition Enemy = null;
        public GameObject EnemyPrefab = null;
        public List<Collider2D> SpawnAreas = new();
        [Min(1)] public int Count = 1;
        [Min(0f)] public float SpawnInterval = 0f;
        public Vector3 PositionOffset = Vector3.zero;
    }

    [Serializable]
    public class WaveDefinition
    {
        public string Name = "Wave";
        public List<EnemySpawnDefinition> Spawns = new();
    }

    [Header("References")]
    [SerializeField] private GridManager gridManager = null;
    [SerializeField] private Transform enemyParent = null;

    [Header("Behaviour")]
    [SerializeField] private bool autoReturnToPlacementWhenCleared = true;
    [SerializeField] private bool repeatLastConfiguredWave = true;
    [SerializeField] [Min(1)] private int spawnAreaSampleAttempts = 24;
    [SerializeField] private List<WaveDefinition> waves = new();

    private readonly List<GameObject> activeEnemies = new();
    private readonly List<Collider2D> spawnAreaBuffer = new();
    private Coroutine spawnRoutine;
    private bool waveActive;
    private int nextWaveNumber = 1;
    private int currentWaveNumber;

    public event Action<int> WaveStarted;
    public event Action<int> WaveCompleted;

    public bool WaveActive => waveActive;
    public int CurrentWaveNumber => currentWaveNumber;

    private void Reset()
    {
        enemyParent = transform;
        gridManager = GetComponent<GridManager>() ?? GetComponentInParent<GridManager>();
    }

    private void Awake()
    {
        if (enemyParent == null)
        {
            enemyParent = transform;
        }

        if (gridManager == null)
        {
            gridManager = GetComponent<GridManager>() ?? GetComponentInParent<GridManager>();
        }

        DisableSceneEnemyTemplates();
    }

    private void Update()
    {
        if (!waveActive || spawnRoutine != null)
        {
            return;
        }

        TryCompleteWave();
    }

    public void StartNextWave()
    {
        if (waveActive)
        {
            Debug.LogWarning("WaveController is already running a wave.", this);
            return;
        }

        if (!TryGetNextWaveDefinition(out WaveDefinition wave))
        {
            Debug.LogWarning("WaveController has no configured waves left to spawn.", this);
            return;
        }

        currentWaveNumber = nextWaveNumber;
        nextWaveNumber++;
        waveActive = true;
        CleanupActiveEnemies();
        gridManager?.HidePlacementGridForWave();
        spawnRoutine = StartCoroutine(SpawnWaveRoutine(wave));
        WaveStarted?.Invoke(currentWaveNumber);
    }

    public void BeginPlacementPhase()
    {
        if (waveActive)
        {
            return;
        }

        gridManager?.ShowPlacementGrid();
    }

    public void ResetWaveProgress()
    {
        nextWaveNumber = 1;
        currentWaveNumber = 0;
    }

    private IEnumerator SpawnWaveRoutine(WaveDefinition wave)
    {
        List<EnemySpawnDefinition> spawns = wave != null ? wave.Spawns : null;
        if (spawns != null)
        {
            for (int spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex++)
            {
                EnemySpawnDefinition spawn = spawns[spawnIndex];
                if (spawn == null || ResolveEnemyPrefab(spawn) == null)
                {
                    continue;
                }

                int spawnCount = Mathf.Max(1, spawn.Count);
                for (int enemyIndex = 0; enemyIndex < spawnCount; enemyIndex++)
                {
                    SpawnEnemy(spawn);

                    bool shouldWait = spawn.SpawnInterval > 0f && enemyIndex < spawnCount - 1;
                    if (shouldWait)
                    {
                        yield return new WaitForSeconds(spawn.SpawnInterval);
                    }
                }
            }
        }

        spawnRoutine = null;
        TryCompleteWave();
    }

    private void TryCompleteWave()
    {
        CleanupActiveEnemies();
        if (activeEnemies.Count > 0)
        {
            return;
        }

        CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        if (!waveActive)
        {
            return;
        }

        waveActive = false;
        if (autoReturnToPlacementWhenCleared)
        {
            gridManager?.ShowPlacementGrid();
        }

        WaveCompleted?.Invoke(currentWaveNumber);
    }

    private bool TryGetNextWaveDefinition(out WaveDefinition wave)
    {
        if (waves == null || waves.Count == 0)
        {
            wave = null;
            return false;
        }

        int requestedIndex = nextWaveNumber - 1;
        if (requestedIndex < waves.Count)
        {
            wave = waves[requestedIndex];
            return true;
        }

        if (repeatLastConfiguredWave)
        {
            wave = waves[waves.Count - 1];
            return true;
        }

        wave = null;
        return false;
    }

    private void SpawnEnemy(EnemySpawnDefinition spawn)
    {
        GameObject enemyPrefab = ResolveEnemyPrefab(spawn);
        if (spawn == null || enemyPrefab == null)
        {
            return;
        }

        Collider2D spawnArea = ResolveSpawnArea(spawn);
        Transform spawnOrigin = ResolveSpawnOrigin(spawn, spawnArea);
        Vector3 spawnPosition = ResolveSpawnPosition(spawnArea, spawnOrigin) + spawn.PositionOffset;
        Quaternion spawnRotation = spawnOrigin != null ? spawnOrigin.rotation : Quaternion.identity;

        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
        if (enemyParent != null)
        {
            enemyInstance.transform.SetParent(enemyParent, true);
        }

        InitializeEnemy(enemyInstance, spawn.Enemy);
        activeEnemies.Add(enemyInstance);
    }

    private Transform ResolveSpawnOrigin(EnemySpawnDefinition spawn, Collider2D spawnArea)
    {

        if (spawnArea != null)
        {
            return spawnArea.transform;
        }
        return transform;
    }

    private Vector3 ResolveSpawnPosition(Collider2D spawnArea, Transform spawnOrigin)
    {
        if (TryGetSpawnPositionFromArea(spawnArea, spawnOrigin, out Vector3 spawnPosition))
        {
            return spawnPosition;
        }

        return spawnOrigin != null ? spawnOrigin.position : transform.position;
    }

    private Collider2D ResolveSpawnArea(EnemySpawnDefinition spawn)
    {
        spawnAreaBuffer.Clear();

        if (spawn != null)
        {
            AddSpawnAreaCandidates(spawn.SpawnAreas);

        }

        if (spawnAreaBuffer.Count == 0)
        {
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, spawnAreaBuffer.Count);
        return spawnAreaBuffer[randomIndex];
    }

    private bool TryGetSpawnPositionFromArea(Collider2D spawnArea, Transform spawnOrigin, out Vector3 spawnPosition)
    {
        spawnPosition = default;
        if (!IsValidSpawnArea(spawnArea))
        {
            return false;
        }

        Bounds bounds = spawnArea.bounds;
        int attempts = Mathf.Max(1, spawnAreaSampleAttempts);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 candidate = new(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y));

            if (!spawnArea.OverlapPoint(candidate))
            {
                continue;
            }

            float spawnZ = spawnOrigin != null ? spawnOrigin.position.z : spawnArea.transform.position.z;
            spawnPosition = new Vector3(candidate.x, candidate.y, spawnZ);
            return true;
        }

        Vector2 center = bounds.center;
        if (spawnArea.OverlapPoint(center))
        {
            float spawnZ = spawnOrigin != null ? spawnOrigin.position.z : spawnArea.transform.position.z;
            spawnPosition = new Vector3(center.x, center.y, spawnZ);
            return true;
        }

        Debug.LogWarning($"WaveController could not find a valid spawn point inside spawn area '{spawnArea.name}'. Falling back to the spawn origin position.", spawnArea);
        return false;
    }

    private static bool IsValidSpawnArea(Collider2D spawnArea)
    {
        return spawnArea != null &&
               spawnArea.enabled &&
               spawnArea.gameObject.activeInHierarchy &&
               spawnArea.bounds.size.x > Mathf.Epsilon &&
               spawnArea.bounds.size.y > Mathf.Epsilon;
    }

    private void AddSpawnAreaCandidates(List<Collider2D> spawnAreas)
    {
        if (spawnAreas == null)
        {
            return;
        }

        for (int i = 0; i < spawnAreas.Count; i++)
        {
            AddSpawnAreaCandidate(spawnAreas[i]);
        }
    }

    private void AddSpawnAreaCandidate(Collider2D spawnArea)
    {
        if (!IsValidSpawnArea(spawnArea) || spawnAreaBuffer.Contains(spawnArea))
        {
            return;
        }

        spawnAreaBuffer.Add(spawnArea);
    }

    private void CleanupActiveEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = activeEnemies[i];
            if (enemy == null || !enemy.activeInHierarchy)
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }

    private void DisableSceneEnemyTemplates()
    {
        if (waves == null)
        {
            return;
        }

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            WaveDefinition wave = waves[waveIndex];
            if (wave == null || wave.Spawns == null)
            {
                continue;
            }

            for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
            {
                EnemySpawnDefinition spawn = wave.Spawns[spawnIndex];
                GameObject enemyPrefab = ResolveEnemyPrefab(spawn);
                if (enemyPrefab == null || !enemyPrefab.scene.IsValid())
                {
                    continue;
                }

                enemyPrefab.SetActive(false);
            }
        }
    }

    private static GameObject ResolveEnemyPrefab(EnemySpawnDefinition spawn)
    {
        if (spawn == null)
        {
            return null;
        }

        if (spawn.Enemy != null && spawn.Enemy.Prefab != null)
        {
            return spawn.Enemy.Prefab;
        }

        return spawn.EnemyPrefab;
    }

    private static void InitializeEnemy(GameObject enemyInstance, EnemyDefinition enemyDefinition)
    {
        if (enemyDefinition == null || enemyInstance == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = enemyInstance.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemy enemy)
            {
                enemy.Initialize(enemyDefinition);
                return;
            }
        }
    }
}
