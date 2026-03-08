using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StairZone : MonoBehaviour
{

    [Tooltip("Altura do TOPO da escada (valor mais alto).")]
    public int topHeight = 1;
    public GameObject StairWall;

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        entity.ForceHeight(topHeight);
        StairWall?.SetActive(true); // Essa parede é ativada para complementar a ilusão de que o personagem está subindo a escada, fazendo uma layer de parede aparecer na frente do personagem caso ele esteja próximo à borda da escada. Ela é desativada quando o personagem sai da escada, para evitar que fique aparecendo na frente do personagem quando ele estiver longe da borda.
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        if (entity.CurrentHeight < topHeight)
            entity.ForceHeight(topHeight);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        entity.ForceHeight(topHeight);
        StairWall?.SetActive(false);
    }
}