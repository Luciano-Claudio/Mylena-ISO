using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StairExitZone : MonoBehaviour
{
    public int bottomHeight = 0;

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        entity.ForceHeight(bottomHeight);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        entity.ForceHeight(bottomHeight);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var entity = other.GetComponentInParent<IsoEntityHeight>();
        if (entity == null) return;
        entity.ForceHeight(bottomHeight);
    }
}