using UnityEngine;

public interface IEnemyDefinition
{
    EnemyType EnemyType { get; }
    string DisplayName { get; }
    EnemyStats Stats { get; }
    EnemyMovementProfile Movement { get; }
    Sprite Sprite { get; }
    GameObject Prefab { get; }
}
