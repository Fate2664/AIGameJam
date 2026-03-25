using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInteractionDetector : MonoBehaviour
{
   [SerializeField] private GameInput gameInput; 
   private PlayerMovement player;
   private IInteractable currentTarget;
   
   private void Awake()
   {
      player = GetComponent<PlayerMovement>();
      gameInput.Interact += OnInteract;
      gameInput.EnableActions();
   }


   private void OnTriggerEnter2D(Collider2D other)
   {
      if (!other.TryGetComponent(out Interactable interactable)) return;
      IndicatorManager indicator = other.GetComponentInChildren<IndicatorManager>();
      if (indicator == null) return;
      
      currentTarget = interactable;
      indicator.ShowIndictor();
   }

   private void OnTriggerExit2D(Collider2D other)
   {
      if (!other.TryGetComponent(out Interactable interactable)) return;
      IndicatorManager indicator = other.GetComponentInChildren<IndicatorManager>();
      currentTarget = null;
      indicator.HideIndictor();
   }

   private void OnInteract(bool pressed)
   {
      if (pressed)
      {
         if (currentTarget != null)
         {
            currentTarget.Interact(player);
         }
      }
   }
   
   private void OnDisable()
   {
      gameInput.Interact -= OnInteract;
   }
}
