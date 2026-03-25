using System;
using System.Collections;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private DialogueVisuals dialogueVisuals;

    private QueueBase<string> sentences = new();
    private bool hasStartedDialogue = false;

    public bool HasStartedDialogue
    {
        get => hasStartedDialogue;
        set => hasStartedDialogue = value;
    }

    private void Start()
    {
        dialogueVisuals.InitializeGestureHandlers();
    }

    public void StartDialogue(DialogueBase dialogue)
    {
        hasStartedDialogue = true;
        dialogueVisuals.NameText.Text = dialogue.DialogueName;
        dialogueVisuals.Show();
        sentences.Clear();
        dialogueVisuals.OnRightArrowPressed += DisplayNextDialogueText;

        foreach (string textBlock in dialogue.DialogueText)
        {
            sentences.Enqueue(textBlock);
        }

        DisplayNextDialogueText();
    }

    private void DisplayNextDialogueText()
    {
        if (sentences.Count == 0)
        {
            EndDialogue();
            return;
        }

        string textToDisplay = (string)sentences.Dequeue();
        dialogueVisuals.DialogueText.Text = textToDisplay;
        StopAllCoroutines();
        StartCoroutine(ShowDialogueText(textToDisplay));
    }

    private IEnumerator ShowDialogueText(string textToDisplay)
    {
        dialogueVisuals.DialogueText.Text = "";
        foreach (char letter in textToDisplay.ToCharArray())
        {
            dialogueVisuals.DialogueText.Text += letter;
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(3f);

        DisplayNextDialogueText();
    }

    private void EndDialogue()
    {
        hasStartedDialogue = false;
        dialogueVisuals.Hide();
        dialogueVisuals.OnRightArrowPressed -= DisplayNextDialogueText;
    }
}