using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerCombat : MonoBehaviour
{
   [Header("Combat Settings")]
   
   [Space(10)]
   [Header("Connections")]
   [SerializeField] private GameInput gameInput;

   public event UnityAction<bool> OnAttack;

   private void Awake()
   {
       gameInput.PrimaryAttack += HandlePrimaryAttack;
       gameInput.EnableActions();
   }

   private void OnDestroy()
   {
       gameInput.PrimaryAttack -= HandlePrimaryAttack;
   }

   private void HandlePrimaryAttack(bool pressed)
   {
       OnAttack?.Invoke(pressed);
   }
   

   
}
