// Jigsaw prototype

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpSpeed = 7f;

    private Rigidbody2D rb;
    private float moveInput;
    private bool jumpRequested;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        moveInput = 0f;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed)
            moveInput -= 1f;

        if (Keyboard.current.dKey.isPressed)
            moveInput += 1f;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpRequested = true;
    }

    private void FixedUpdate()
    {
        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInput * moveSpeed;

        if (jumpRequested && IsGrounded() && velocity.y <= 0.1f)
            velocity.y = jumpSpeed;

        rb.linearVelocity = velocity;
        jumpRequested = false;
    }

    private bool IsGrounded()
    {
        int count = rb.GetContacts(contacts);

        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y > 0.5f)
                return true;
        }

        return false;
    }
}