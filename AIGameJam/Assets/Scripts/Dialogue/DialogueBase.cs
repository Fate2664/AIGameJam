using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "Dialogue/DialogueBase")]
public class DialogueBase : ScriptableObject
{
    public string DialogueName;
    [TextArea(3,10)]
    public string[] DialogueText;
}
