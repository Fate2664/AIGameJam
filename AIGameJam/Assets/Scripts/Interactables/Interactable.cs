using System;
using Nova;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] private IndicatorManager indicatorManager;

    private bool hasInteracted;

    public void Interact(PlayerMovement interactor)
    {
        hasInteracted  = true;
        Debug.Log("Interacted");
    }
}
