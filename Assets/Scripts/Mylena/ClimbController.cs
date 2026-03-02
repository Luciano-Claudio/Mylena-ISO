using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Gerencia a lógica de climbing do personagem.
/// Comunica com MovementController, AnimationController e IsoEntityHeight.
/// </summary>
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(IsoEntityHeight))]
public class ClimbController : MonoBehaviour
{
    [Header("Climb Config")]
    [Tooltip("Duração total do arco de climb em segundos.")]
    [SerializeField] private float climbDuration = 0.4f;

    [Tooltip("Altura do arco visual durante o climb (em unidades).")]
    [SerializeField] private float climbArcHeight = 0.5f;

    [Tooltip("Delay antes do climb começar (anticipation).")]
    [SerializeField] private float climbDelay = 0.2f;

    [Tooltip("Tempo para mudar o Height]")]
    [SerializeField] private float timeHeight = 0.5f;

    [Tooltip("Tempo de cooldown após um climb — impede re-ativação imediata.")]
    [SerializeField] private float climbCooldown = 1f;
    private float _lastClimbEndTime = -99f;

    // ─── Estado ────────────────────────────────────────────────────────────
    public bool IsClimbing { get; private set; } = false;
    public bool IsOnCooldown => Time.time - _lastClimbEndTime < climbCooldown;
    public bool CanClimb => !IsClimbing && !IsOnCooldown;

    // ─── Referências ───────────────────────────────────────────────────────
    private MovementController _movement;
    private AnimationController _anim;
    private IsoEntityHeight _heightEntity;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _movement = GetComponent<MovementController>();
        _anim = GetComponentInChildren<AnimationController>();
        _heightEntity = GetComponent<IsoEntityHeight>();
        _rb = GetComponentInChildren<Rigidbody2D>();
    }

    /// <summary>
    /// Inicia o climb. Chamado pelo ClimbTrigger.
    /// </summary>
    /// <param name="destination">Posição de destino no mundo.</param>
    /// <param name="ascending">True = subindo, False = descendo.</param>
    /// <param name="climbDirection">Direção visual do climb para animação.</param>
    public void StartClimb(Vector2 destination, bool ascending, Vector2 climbDirection, IsoDirection8 animDir)
    {
        if (IsClimbing) return;
        StartCoroutine(ClimbRoutine(destination, ascending, climbDirection, animDir));
    }

    private IEnumerator ClimbRoutine(Vector2 destination, bool ascending, Vector2 climbDirection, IsoDirection8 animDir)
    {
        IsClimbing = true;

        _movement.SetClimbLock(true);
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        // Passa direção exata para o AnimationController
        _anim?.SetClimb(true, climbDirection, animDir);

        yield return new WaitForSeconds(climbDelay);

        Vector2 startPos = transform.position;
        float elapsed = 0f;
        bool heightChanged = false;

        while (elapsed < climbDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / climbDuration);

            float arc = Mathf.Sin(t * Mathf.PI) * climbArcHeight;
            Vector2 flatPos = Vector2.Lerp(startPos, destination, t);
            transform.position = new Vector3(flatPos.x, flatPos.y + arc, 0f);

            if (!heightChanged && t >= timeHeight)
            {
                heightChanged = true;
                if (ascending) _heightEntity.Ascend();
                else _heightEntity.Descend();
            }

            yield return null;
        }

        transform.position = new Vector3(destination.x, destination.y, 0f);

        _anim?.SetClimb(false, climbDirection, animDir);
        _movement.SetClimbLock(false);

        _lastClimbEndTime = Time.time; // registra quando terminou

        IsClimbing = false;
    }

}