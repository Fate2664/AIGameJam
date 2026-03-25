using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Space(10)]
    [Range(1f, 10f)]
    [SerializeField] private float moveSpeed;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    
    [Space(10)]
    [Header("Connections")]
    [SerializeField] private GameInput gameInput;
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isWalking;
    private bool isGrounded;
    private bool isJumping => !isGrounded;
    private float lastMoveX;
    
    private void OnMove(Vector2 dir) => moveInput = dir;
    public bool IsWalking() => isWalking;
    public bool IsJumping() => isJumping;
    public float GetFacingDir() => lastMoveX;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();      
        gameInput.Move += OnMove;
        gameInput.EnableActions();
    }

    
    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
        HandleMovement();
    }


    private void HandleMovement()
    {
        Vector2 inputVector = moveInput;
        
        rb.linearVelocity = new Vector2 (inputVector.x * moveSpeed, rb.linearVelocity.y);
        
        isWalking = Mathf.Abs(inputVector.x) > 0.01f;
        
        if (isWalking)
        {
            lastMoveX = -inputVector.x;
        }
    }
    
    private void OnDestroy()
    {
        gameInput.Move -= OnMove;
    }
}

