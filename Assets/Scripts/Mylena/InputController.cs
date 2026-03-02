using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputController : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;

    public bool DebugMode = false;
    public Vector2 MoveInput { get; private set; }

    // Tempo máximo que o jump fica válido sem ser consumido (em segundos)
    private const float JUMP_BUFFER_TIME = 0.1f;
    private float _jumpTimestamp = -1f;

    public bool JumpPressed => Time.time - _jumpTimestamp <= JUMP_BUFFER_TIME;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
    }

    /// <summary>
    /// Consome o jump. Retorna true se havia jump válido.
    /// Invalida imediatamente após consumo.
    /// </summary>
    public bool ConsumeJump()
    {
        if (!JumpPressed) return false;
        _jumpTimestamp = -1f; // invalida imediatamente
        return true;
    }

    private void OnEnable()
    {
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        jumpAction.performed += OnJump;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        jumpAction.performed -= OnJump;
    }

    private void OnMove(InputAction.CallbackContext ctx) => MoveInput = ctx.ReadValue<Vector2>();
    private void OnJump(InputAction.CallbackContext ctx) => _jumpTimestamp = Time.time;
}