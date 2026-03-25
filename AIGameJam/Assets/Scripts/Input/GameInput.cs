using System;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Input/Game Input")]
public class GameInput : ScriptableObject, PlayerInputActions.IPlayerActions, PlayerInputActions.IUIActions 
{
    //Player Actions
    public event UnityAction<Vector2> Move =  delegate { };
    public event UnityAction<bool> PrimaryAttack  =  delegate { };
    public event UnityAction<bool> Interact  =  delegate { };
    
    //UI Actions
    public event UnityAction<bool> Exit  =  delegate { };
    public event UnityAction<bool> RestoreDefaults  =  delegate { };
    public event UnityAction<bool> Apply  =  delegate { };
    public event UnityAction<float> VerticalNav  =  delegate { };
    public event UnityAction<float> HorizontalNav  =  delegate { };
    public event UnityAction<float> TabNav = delegate { };


    private PlayerInputActions inputActions;
    
    
    public Vector2 Direction => inputActions.Player.Move.ReadValue<Vector2>();
    public bool IsPrimaryAttackPressed => inputActions.Player.PrimaryAttack.IsPressed();
    public bool IsInteractPressed => inputActions.Player.Interact.IsPressed();
    
    
    public void EnableActions()
    {
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();
            inputActions.Player.SetCallbacks(this);
            inputActions.UI.SetCallbacks(this);
        }    
        
        inputActions.Player.Enable();
        inputActions.UI.Enable();
    }
    
    public void OnMove(InputAction.CallbackContext context)
    {
        Move.Invoke(context.ReadValue<Vector2>());
    }

    void PlayerInputActions.IPlayerActions.OnInteract(InputAction.CallbackContext context)
    {
        Interact.Invoke(context.phase == InputActionPhase.Performed);
    }

    void PlayerInputActions.IPlayerActions.OnPrimaryAttack(InputAction.CallbackContext context)
    {
        PrimaryAttack.Invoke(context.phase == InputActionPhase.Performed);
    }

    void PlayerInputActions.IUIActions.OnExit(InputAction.CallbackContext context)
    {
        Exit.Invoke(context.phase == InputActionPhase.Performed);
    }


    public void OnVerticalNavigation(InputAction.CallbackContext context)
    {
        VerticalNav.Invoke(context.ReadValue<float>());
    }

    public void OnHorizontalNavigation(InputAction.CallbackContext context)
    {
        HorizontalNav.Invoke(context.ReadValue<float>());
    }

    void PlayerInputActions.IUIActions.OnRestoreDefaults(InputAction.CallbackContext context)
    {
        RestoreDefaults.Invoke(context.phase == InputActionPhase.Performed);
    }

    void PlayerInputActions.IUIActions.OnApply(InputAction.CallbackContext context)
    {
        Apply.Invoke(context.phase == InputActionPhase.Performed);
    }

    public void OnTabNavigation(InputAction.CallbackContext context)
    {
        TabNav.Invoke(context.ReadValue<float>());
    }
}
