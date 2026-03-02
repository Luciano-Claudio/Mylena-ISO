using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ClimbTrigger : MonoBehaviour
{
    public enum ClimbType { Up, Down }

    [Header("Config")]
    public ClimbType climbType = ClimbType.Up;
    public Transform destination;
    public IsoDirection8 climbAnimationDirection = IsoDirection8.NE;

    private ClimbController _climbCtrl;
    private InputController _input;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var climb = other.GetComponentInParent<ClimbController>();
        if (climb == null) return;
        _climbCtrl = climb;
        _input = other.GetComponentInParent<InputController>();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var climb = other.GetComponentInParent<ClimbController>();
        if (climb != _climbCtrl) return;
        _climbCtrl = null;
        _input = null;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (_climbCtrl == null || !_climbCtrl.CanClimb) return;
        if (_input == null) return;
        if (!_input.ConsumeJump()) return;

        if (destination == null)
        {
            Debug.LogWarning("[ClimbTrigger] Destination não configurado!", this);
            return;
        }

        Vector2 climbDir = Direction8ToVector(climbAnimationDirection);
        bool ascending = climbType == ClimbType.Up;

        // ← Guarda referências locais e limpa ANTES de iniciar
        var ctrl = _climbCtrl;
        _climbCtrl = null;
        _input = null;

        ctrl.StartClimb(destination.position, ascending, climbDir, climbAnimationDirection);
    }

    private Vector2 Direction8ToVector(IsoDirection8 dir)
    {
        return dir switch
        {
            IsoDirection8.N => new Vector2(0, 1),
            IsoDirection8.NE => new Vector2(1, 1),
            IsoDirection8.E => new Vector2(1, 0),
            IsoDirection8.SE => new Vector2(1, -1),
            IsoDirection8.S => new Vector2(0, -1),
            IsoDirection8.SW => new Vector2(-1, -1),
            IsoDirection8.W => new Vector2(-1, 0),
            IsoDirection8.NW => new Vector2(-1, 1),
            _ => Vector2.up
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (destination == null) return;
        Gizmos.color = climbType == ClimbType.Up ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawSphere(destination.position, 0.1f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f,
            $"Climb {climbType} [Space]\nAnim Dir: {climbAnimationDirection}");
    }
#endif
}