using System;
using UnityEngine;

[Serializable]
public struct EnemyMovementProfile
{
    [Min(0f)] public float LateralWobbleAmplitude;
    [Min(0.01f)] public float LateralWobbleFrequency;
    [Min(0f)] public float NoiseAmplitude;
    [Min(0.01f)] public float NoiseFrequency;

    public EnemyMovementProfile(float lateralWobbleAmplitude, float lateralWobbleFrequency, float noiseAmplitude, float noiseFrequency)
    {
        LateralWobbleAmplitude = Mathf.Max(0f, lateralWobbleAmplitude);
        LateralWobbleFrequency = Mathf.Max(0.01f, lateralWobbleFrequency);
        NoiseAmplitude = Mathf.Max(0f, noiseAmplitude);
        NoiseFrequency = Mathf.Max(0.01f, noiseFrequency);
    }

    public static EnemyMovementProfile Default => new(0.15f, 1.35f, 0.05f, 0.75f);
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Enemies/Enemy Definition")]
public class EnemyDefinition : ScriptableObject, IEnemyDefinition
{
    [SerializeField] private string displayName = "Enemy";
    [SerializeField] private EnemyType enemyType = EnemyType.Base;
    [SerializeField] private GameObject prefab = null;
    [SerializeField] private Sprite sprite = null;
    [SerializeField] private EnemyStats stats = new(10f, 1f, 2f, 0.75f);
    [SerializeField] private EnemyMovementProfile movement = EnemyMovementProfile.Default;

    public EnemyType EnemyType => enemyType;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public EnemyStats Stats => stats;
    public EnemyMovementProfile Movement => movement;
    public Sprite Sprite => sprite;
    public GameObject Prefab => prefab;
}
