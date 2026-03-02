using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente central do sistema IsoSort Pro.
/// Adicione em qualquer sprite que precisa de ordenação isométrica.
/// </summary>
public class IsoSortable : MonoBehaviour
{
    // ─── Configuração de Altura ───────────────────────────────────────────

    [Header("Height")]
    [Tooltip("Em qual andar este objeto começa. 0 = chão, 1 = platô, 2 = segundo platô...")]
    public int logicalHeight = 0;

    [Tooltip("Quantos andares este objeto ocupa visualmente. Árvore que vai do h0 até h1 = span de 2.")]
    [Min(1)]
    public int heightSpan = 1;

    // ─── Configuração de Footprint ────────────────────────────────────────

    public enum FootprintType { Point, Line, Polyline }

    [Tooltip("Pontos da Polyline (mínimo 2). Ignorado se Point ou Line.")]
    public Vector2[] polylinePoints = new Vector2[]
    {
    new Vector2(-0.5f, 0f),
    new Vector2( 0.5f, 0f)
    };

    [Header("Footprint (Sorting Shape)")]
    [Tooltip("Point: objetos menores (NPCs, barris). Line: objetos com largura (blocos, troncos caídos).")]
    public FootprintType footprintType = FootprintType.Point;

    [Tooltip("Offset do pivot de sorting em relação à posição do transform.")]
    public Vector2 footprintOffset = Vector2.zero;

    [Tooltip("Segundo ponto do Line footprint (ignorado se Point).")]
    public Vector2 footprintOffset2 = new Vector2(0.5f, 0f);

    // ─── Flags ────────────────────────────────────────────────────────────

    [Header("Behavior")]
    [Tooltip("O objeto se move? (Player, NPC = true. Árvore, bloco = false)")]
    public bool isMovable = false;

    [Tooltip("Sempre renderiza abaixo de tudo (sombras projetadas no chão).")]
    public bool renderBelowAll = false;

    // ─── Renderers ────────────────────────────────────────────────────────

    [Header("Renderers")]
    [Tooltip("Deixe vazio para auto-detectar todos os Renderers filhos.")]
    public Renderer[] renderersToSort = Array.Empty<Renderer>();

    // ─── Runtime (não serializado) ────────────────────────────────────────

    [NonSerialized] public bool registered = false;
    [NonSerialized] public bool forceSort = false;

    // Pontos de sorting calculados em world space
    [NonSerialized] public Vector2 worldPoint1;
    [NonSerialized] public Vector2 worldPoint2;
    [NonSerialized] public Bounds2D cachedBounds;

    // Dependências para topological sort
    [NonSerialized] public readonly List<IsoSortable> staticDeps = new List<IsoSortable>(16);
    [NonSerialized] public readonly List<IsoSortable> inverseStaticDeps = new List<IsoSortable>(16);
    [NonSerialized] public readonly List<IsoSortable> movingDeps = new List<IsoSortable>(8);

    // ─── Cache interno ────────────────────────────────────────────────────

    private Transform _t;
    private int _lastCacheFrame = -1;

    // ─── Propriedades ─────────────────────────────────────────────────────

    /// <summary>Altura máxima que este objeto ocupa (logicalHeight + span - 1).</summary>
    public int MaxHeight => logicalHeight + heightSpan - 1;

    /// <summary>Ponto médio do footprint em world space.</summary>
    /// 
    public Vector2 FootprintCenter
    {
        get
        {
            if (footprintType == FootprintType.Line)
                return (worldPoint1 + worldPoint2) * 0.5f;

            if (footprintType == FootprintType.Polyline && worldPolylinePoints != null && worldPolylinePoints.Length > 0)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < worldPolylinePoints.Length; i++)
                    sum += worldPolylinePoints[i];
                return sum / worldPolylinePoints.Length;
            }

            return worldPoint1;
        }
    }

    public int SortingOrder
    {
        set
        {
            int count = renderersToSort.Length;
            for (int i = 0; i < count; i++)
            {
                renderersToSort[i].sortingLayerName = "World";
                renderersToSort[i].sortingOrder = value + i;
            }
        }
        get => renderersToSort.Length > 0 ? renderersToSort[0].sortingOrder : 0;
    }

    // ─── Unity ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _t = transform;
    }

    private System.Collections.IEnumerator Start()
    {
        if (!Application.isPlaying) yield break;
        _ = IsoSortingManager.Instance; // garante que o manager existe
        yield return null;              // espera 1 frame para todos Awakes rodarem
        Initialize();
    }

    private void OnEnable()
    {
        RefreshCache();
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
            IsoSortingManager.Unregister(this);
    }

    // ─── Público ──────────────────────────────────────────────────────────

    // Adicione este campo nos NonSerialized:
    [NonSerialized] public Vector2[] worldPolylinePoints;

    public void RefreshCache()
    {
        if (_t == null) _t = transform;

        Vector2 pos = _t.position;
        worldPoint1 = pos + footprintOffset;
        worldPoint2 = pos + footprintOffset2;

        // Atualiza polyline em world space
        if (footprintType == FootprintType.Polyline && polylinePoints != null)
        {
            if (worldPolylinePoints == null || worldPolylinePoints.Length != polylinePoints.Length)
                worldPolylinePoints = new Vector2[polylinePoints.Length];

            for (int i = 0; i < polylinePoints.Length; i++)
                worldPolylinePoints[i] = pos + polylinePoints[i];
        }

        if (renderersToSort.Length > 0)
            cachedBounds = new Bounds2D(renderersToSort[0].bounds);

        _lastCacheFrame = Time.frameCount;
    }

    public bool NeedsRefresh()
    {
        return isMovable && _t.hasChanged && _lastCacheFrame < Time.frameCount;
    }

    public void ClearChangedFlag()
    {
        if (_t != null) _t.hasChanged = false;
    }

    // ─── Privado ──────────────────────────────────────────────────────────

    private void Initialize()
    {
        _t = transform;

        if (renderersToSort == null || renderersToSort.Length == 0)
            renderersToSort = GetComponentsInChildren<Renderer>(true);

        RefreshCache();
        IsoSortingManager.Register(this);
    }

    // ─── Comparação de Sorting ────────────────────────────────────────────

    /// <summary>
    /// Retorna -1 se A deve renderizar ANTES de B (atrás), +1 se A deve renderizar DEPOIS (na frente), 0 se igual.
    /// </summary>
    public static int Compare(IsoSortable a, IsoSortable b)
    {
        if (a == b) return 0;

        bool heightsOverlap = a.logicalHeight <= b.MaxHeight && b.logicalHeight <= a.MaxHeight;

        if (!heightsOverlap)
        {
            // Sem sobreposição de altura: maior height = mais na frente
            return a.logicalHeight < b.logicalHeight ? -1 : 1;
        }

        // Alturas se sobrepõem: usa posição geométrica
        return CompareGeometric(a, b);
    }

    private static int CompareGeometric(IsoSortable a, IsoSortable b)
    {
        // Point vs Point
        if (a.footprintType == FootprintType.Point && b.footprintType == FootprintType.Point)
            return b.worldPoint1.y.CompareTo(a.worldPoint1.y);

        // Line vs Line
        if (a.footprintType == FootprintType.Line && b.footprintType == FootprintType.Line)
            return CompareLineVsLine(a, b);

        // Point vs Line
        if (a.footprintType == FootprintType.Point && b.footprintType == FootprintType.Line)
            return ComparePointVsLine(a.worldPoint1, b);

        // Line vs Point
        if (a.footprintType == FootprintType.Line && b.footprintType == FootprintType.Point)
            return -ComparePointVsLine(b.worldPoint1, a);

        // Point vs Polyline
        if (a.footprintType == FootprintType.Point && b.footprintType == FootprintType.Polyline)
            return ComparePointVsPolyline(a.worldPoint1, b.worldPolylinePoints);

        // Polyline vs Point
        if (a.footprintType == FootprintType.Polyline && b.footprintType == FootprintType.Point)
            return -ComparePointVsPolyline(b.worldPoint1, a.worldPolylinePoints);

        // Polyline vs Polyline — usa centros como fallback
        if (a.footprintType == FootprintType.Polyline && b.footprintType == FootprintType.Polyline)
            return b.FootprintCenter.y.CompareTo(a.FootprintCenter.y);

        // Line vs Polyline
        if (a.footprintType == FootprintType.Line && b.footprintType == FootprintType.Polyline)
            return ComparePointVsPolyline(a.FootprintCenter, b.worldPolylinePoints);

        // Polyline vs Line
        if (a.footprintType == FootprintType.Polyline && b.footprintType == FootprintType.Line)
            return -ComparePointVsPolyline(b.FootprintCenter, a.worldPolylinePoints);

        return 0;
    }
    private static int ComparePointVsPolyline(Vector2 point, Vector2[] polyline)
    {
        if (polyline == null || polyline.Length < 2) return 0;

        float px = point.x;
        float py = point.y;

        // Encontra o segmento da polyline mais próximo horizontalmente do ponto
        for (int i = 0; i < polyline.Length - 1; i++)
        {
            Vector2 p1 = polyline[i];
            Vector2 p2 = polyline[i + 1];

            float minX = Mathf.Min(p1.x, p2.x);
            float maxX = Mathf.Max(p1.x, p2.x);

            // Ponto está na faixa X deste segmento?
            if (px >= minX && px <= maxX)
            {
                // Interpola Y na linha entre p1 e p2
                float dx = p2.x - p1.x;
                float t = Mathf.Abs(dx) < 0.0001f ? 0.5f : (px - p1.x) / dx;
                float yOnLine = Mathf.Lerp(p1.y, p2.y, t);

                return py < yOnLine ? 1 : -1;
            }
        }

        // Fora da faixa X da polyline: compara com extremo mais próximo
        Vector2 leftmost = polyline[0];
        Vector2 rightmost = polyline[polyline.Length - 1];

        float refY = px < leftmost.x ? leftmost.y : rightmost.y;
        return py < refY ? 1 : -1;
    }

    private static int ComparePointVsLine(Vector2 point, IsoSortable line)
    {
        float py = point.y;
        float ly1 = line.worldPoint1.y;
        float ly2 = line.worldPoint2.y;

        if (py > ly1 && py > ly2) return -1; // ponto acima da linha → atrás
        if (py < ly1 && py < ly2) return 1;  // ponto abaixo da linha → na frente

        // Ponto está na faixa Y da linha: interpola
        float dx = line.worldPoint2.x - line.worldPoint1.x;
        if (Mathf.Abs(dx) < 0.0001f)
            return py > (ly1 + ly2) * 0.5f ? -1 : 1;

        float slope = (ly2 - ly1) / dx;
        float intercept = ly1 - slope * line.worldPoint1.x;
        float yOnLine = slope * point.x + intercept;

        return yOnLine > py ? 1 : -1;
    }

    private static int CompareLineVsLine(IsoSortable lineA, IsoSortable lineB)
    {
        int c1 = ComparePointVsLine(lineA.worldPoint1, lineB);
        int c2 = ComparePointVsLine(lineA.worldPoint2, lineB);

        if (c1 == c2) return c1;

        int c3 = ComparePointVsLine(lineB.worldPoint1, lineA);
        int c4 = ComparePointVsLine(lineB.worldPoint2, lineA);

        if (c3 == c4) return -c3;

        // Linhas se cruzam: usa centro Y como desempate
        float centerA = (lineA.worldPoint1.y + lineA.worldPoint2.y) * 0.5f;
        float centerB = (lineB.worldPoint1.y + lineB.worldPoint2.y) * 0.5f;
        return centerB.CompareTo(centerA);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 pos = transform.position;
        Vector2 p1 = pos + footprintOffset;

        if (footprintType == FootprintType.Point)
        {
            Gizmos.color = isMovable ? Color.cyan : Color.yellow;
            Gizmos.DrawSphere(p1, 0.07f);
        }
        else if (footprintType == FootprintType.Polyline && polylinePoints != null)
        {
            Vector2 pos2 = transform.position;
            Gizmos.color = Color.magenta;
            for (int i = 0; i < polylinePoints.Length; i++)
            {
                Vector2 wp = pos2 + polylinePoints[i];
                Gizmos.DrawSphere(wp, 0.05f);
                if (i < polylinePoints.Length - 1)
                    Gizmos.DrawLine(wp, pos2 + polylinePoints[i + 1]);
            }
        }
        else
        {
            Vector2 p2 = pos + footprintOffset2;
            Gizmos.color = isMovable ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawSphere(p1, 0.05f);
            Gizmos.DrawSphere(p2, 0.05f);
        }

        // Mostra a banda de altura
        UnityEditor.Handles.Label(pos + Vector2.up * 0.3f,
            $"h{logicalHeight}" + (heightSpan > 1 ? $"→h{MaxHeight}" : ""));
    }
#endif
}