using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Calcula automaticamente a polyline de sorting
/// a partir da borda sul de um Tilemap isométrico.
/// Adicione junto com o IsoSortable no H1_SortBoundary.
/// </summary>
[RequireComponent(typeof(IsoSortable))]
public class IsoTilemapBoundary : MonoBehaviour
{
    [Tooltip("O Tilemap cujas bordas queremos calcular.")]
    public Tilemap tilemap;

    [Tooltip("Offset Y aplicado à borda (ajuste fino para alinhar com o visual).")]
    public float yBias = 0f;

    private IsoSortable _sortable;

    private void Awake()
    {
        _sortable = GetComponent<IsoSortable>();
    }

    private void Start()
    {
        if (tilemap != null)
            BuildBoundary();
    }

    [ContextMenu("Rebuild Boundary")]
    public void BuildBoundary()
    {
        if (tilemap == null) return;

        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;

        var tileSet = new HashSet<Vector2Int>();
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                if (tilemap.HasTile(new Vector3Int(x, y, 0)))
                    tileSet.Add(new Vector2Int(x, y));

        if (tileSet.Count == 0) return;

        Vector3 cellSize = tilemap.cellSize;
        float hw = cellSize.x * 0.5f;
        float hh = cellSize.y * 0.5f;

        // Para cada X, guarda apenas o ponto mais ao sul (menor Y)
        var byX = new Dictionary<float, float>();

        foreach (var cell in tileSet)
        {
            bool hasSW = tileSet.Contains(new Vector2Int(cell.x - 1, cell.y - 1));
            bool hasSE = tileSet.Contains(new Vector2Int(cell.x, cell.y - 1));

            if (hasSW && hasSE) continue;

            Vector3 center = tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            float west = center.x - hw;
            float east = center.x + hw;
            float south = center.y - hh + yBias;
            float mid = center.y + yBias;

            void TryAdd(float px, float py)
            {
                if (!byX.ContainsKey(px) || py < byX[px])
                    byX[px] = py;
            }

            if (!hasSW && !hasSE)
            {
                TryAdd(west, mid);
                TryAdd(center.x, south);
                TryAdd(east, mid);
            }
            else if (!hasSW)
            {
                TryAdd(west, mid);
                TryAdd(center.x, south);
            }
            else
            {
                TryAdd(center.x, south);
                TryAdd(east, mid);
            }
        }

        if (byX.Count == 0)
        {
            Debug.LogWarning("[IsoTilemapBoundary] Nenhuma borda encontrada!");
            return;
        }

        var sortedKeys = new List<float>(byX.Keys);
        sortedKeys.Sort();

        var points = new Vector2[sortedKeys.Count];
        for (int i = 0; i < sortedKeys.Count; i++)
            points[i] = new Vector2(sortedKeys[i], byX[sortedKeys[i]]) - (Vector2)transform.position;

        _sortable.footprintType = IsoSortable.FootprintType.Polyline;
        _sortable.polylinePoints = points;
        _sortable.RefreshCache();

        Debug.Log($"[IsoTilemapBoundary] {points.Length} pontos.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_sortable == null) _sortable = GetComponent<IsoSortable>();
        if (_sortable?.worldPolylinePoints == null) return;

        Gizmos.color = Color.red;
        var pts = _sortable.worldPolylinePoints;
        for (int i = 0; i < pts.Length; i++)
        {
            Gizmos.DrawSphere(pts[i], 0.06f);
            if (i < pts.Length - 1)
                Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }
#endif
}