using System.Collections.Generic;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
   [SerializeField] private DialogueManager DialogueManager;
   [SerializeField] private List<DialogueBase> dialogueList;

   private void OnTriggerEnter2D(Collider2D other)
   {
      if (!other.CompareTag("Player") && DialogueManager.HasStartedDialogue) return;
      DialogueManager.StartDialogue(dialogueList[0]);
   }

   private void OnTriggerExit2D(Collider2D other)
   {
      if (!other.CompareTag("Player")) return;
   }
}
