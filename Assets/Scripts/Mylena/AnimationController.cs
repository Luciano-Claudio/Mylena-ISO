using UnityEngine;

public enum IsoDirection8 : int
{
    S = 0, SW = 1, W = 2, NW = 3,
    N = 4, NE = 5, E = 6, SE = 7
}

public class AnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator anim;

    [Header("Read Only (Debug)")]
    [SerializeField] private IsoDirection8 currentDir = IsoDirection8.S;
    [SerializeField] private bool isMoving;
    [SerializeField] private Vector2 lastDir = Vector2.down;

    private static readonly int DIR = Animator.StringToHash("DIR");
    private static readonly int DIRX = Animator.StringToHash("DIRX");
    private static readonly int DIRY = Animator.StringToHash("DIRY");
    private static readonly int MOVING = Animator.StringToHash("MOVING");
    private static readonly int MOVEX = Animator.StringToHash("MoveX");
    private static readonly int MOVEY = Animator.StringToHash("MoveY");
    private static readonly int CLIMB = Animator.StringToHash("CLIMB");

    private void Awake()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);

        if (anim == null)
            Debug.LogError($"[AnimationController] Nenhum Animator encontrado.", this);

        SetDirection(lastDir);
        anim.SetBool(MOVING, false);
    }

    public void SetMovement(Vector2 move)
    {
        if (anim == null) return;

        isMoving = move.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            lastDir = move.normalized;
            SetDirection(lastDir);
            currentDir = GetDirection8(move);
        }
        else
        {
            SetDirection(lastDir);
        }

        anim.SetBool(MOVING, isMoving);
        //anim.SetFloat(DIR, (float)currentDir);
    }

    /// <summary>Ativa ou desativa a animação de climb.</summary>
    public void SetClimb(bool climbing, Vector2 climbDir, IsoDirection8 animDir)
    {
        if (anim == null) return;

        anim.SetBool(CLIMB, climbing);

        if (climbing)
        {
            // Força direção da animação para o climb
            lastDir = climbDir.normalized;
            SetDirection(lastDir);
            currentDir = GetDirection8(climbDir);
            anim.SetFloat(DIRX, climbDir.x);
            anim.SetFloat(DIRY, climbDir.y);
            anim.SetBool(MOVING, false);
        }
    }

    private IsoDirection8 GetDirection8(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) return IsoDirection8.E;
        if (angle < 67.5f) return IsoDirection8.NE;
        if (angle < 112.5f) return IsoDirection8.N;
        if (angle < 157.5f) return IsoDirection8.NW;
        if (angle < 202.5f) return IsoDirection8.W;
        if (angle < 247.5f) return IsoDirection8.SW;
        if (angle < 292.5f) return IsoDirection8.S;
        return IsoDirection8.SE;
    }

    private void SetDirection(Vector2 dir)
    {
        anim.SetFloat(MOVEX, dir.x);
        anim.SetFloat(MOVEY, dir.y);
    }
}