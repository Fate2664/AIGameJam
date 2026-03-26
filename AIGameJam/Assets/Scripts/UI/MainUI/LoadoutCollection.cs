using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Loadout/Collection")]
public class LoadoutCollection : ScriptableObject
{
    public string CollectionName = "Default Loadout";
    public List<LoadoutItemDefinition> Items = new();
}
