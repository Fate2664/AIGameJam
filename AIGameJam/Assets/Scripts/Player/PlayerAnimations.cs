using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerAnimations : MonoBehaviour
{
    private const string IS_WALKING = "isWalking";
    private const string IS_JUMPING = "isJumping";
    private const string ATTACK1 = "Attack1";
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerCombat  playerCombat;
    
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        playerCombat.OnAttack += PlayPrimaryAttack;
    }

    private void Update()
    {
        animator.SetBool(IS_WALKING, playerMovement.IsWalking());
        animator.SetBool(IS_JUMPING, playerMovement.IsJumping());
        
        float dir = playerMovement.GetFacingDir();
        if (dir != 0)
        {
            spriteRenderer.flipX = dir < 0;
        }
      
    }

    private void OnDestroy()
    {
        if (playerCombat != null)
            playerCombat.OnAttack -= PlayPrimaryAttack;
    }

    private void PlayPrimaryAttack(bool pressed)
    {
        if (pressed)
            animator.SetTrigger(ATTACK1);
    }
}
