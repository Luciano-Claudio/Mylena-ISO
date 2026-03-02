using UnityEngine;

public class MovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputController input;
    [SerializeField] private CharacterController character;
    [SerializeField] private AnimationController anim;
    [SerializeField] private Rigidbody2D rb;

    [Header("Tuning")]
    [SerializeField] private bool normalizeDiagonal = true;

    private Vector2 move;
    private bool _climbLocked = false;

    private void Awake()
    {
        if (input == null) input = GetComponent<InputController>();
        if (character == null) character = GetComponent<CharacterController>();
        if (anim == null) anim = GetComponent<AnimationController>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>(true);

        if (rb == null)
            Debug.LogError("[MovementController] Rigidbody2D não encontrado.", this);
    }

    /// <summary>
    /// Bloqueia/desbloqueia o movimento (usado pelo ClimbController).
    /// </summary>
    public void SetClimbLock(bool locked)
    {
        _climbLocked = locked;
        if (locked && rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (_climbLocked) return;

        move = input != null ? input.MoveInput : Vector2.zero;

        if (normalizeDiagonal && move.sqrMagnitude > 1f)
            move = move.normalized;

        anim?.SetMovement(move);
    }

    private void FixedUpdate()
    {
        if (rb == null || character == null || _climbLocked) return;

        rb.linearVelocity = move * character.RunSpeed;
    }
}