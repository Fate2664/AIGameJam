using UnityEngine;

[CreateAssetMenu(menuName = "Loadout/Item")]
public class LoadoutItemDefinition : ScriptableObject
{
    public string DisplayName = "New Loadout Item";
    public Sprite Icon = null;
    [Min(0)] public int Cost = 0;
    public bool CanRotate = true;
    public GameObject PlaceablePrefab = null;
}
