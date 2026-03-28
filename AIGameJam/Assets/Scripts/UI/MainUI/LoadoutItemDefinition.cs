using UnityEngine;

public enum LoadoutItemType
{
    Trap = 0,
    Wall = 1,
}

[CreateAssetMenu(menuName = "Loadout/Item")]
public class LoadoutItemDefinition : ScriptableObject
{
    public string DisplayName = "New Loadout Item";
    public Sprite Icon = null;
    [Min(0)] public int Cost = 0;
    public LoadoutItemType ItemType = LoadoutItemType.Trap;
    [Min(0f)] public float WallHealth = 40f;
    public bool CanRotate = true;
    public GameObject PlaceablePrefab = null;

    public bool UsesHealth => ItemType == LoadoutItemType.Wall && WallHealth > 0f;
    public float MaxHealth => UsesHealth ? Mathf.Max(1f, WallHealth) : 0f;
}
