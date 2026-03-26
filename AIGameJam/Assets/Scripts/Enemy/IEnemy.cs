public interface IEnemy
{
    IEnemyDefinition Definition { get; }
    float CurrentHealth { get; }
    float MaxHealth { get; }
    float Damage { get; }
    float MovementSpeed { get; }

    void Initialize(EnemyDefinition definition);
}
