using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia e aplica a ordenação isométrica de todos os IsoSortable registrados.
/// Se auto-cria na cena. Não precisa ser colocado manualmente.
/// </summary>
public class IsoSortingManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────

    private static IsoSortingManager _instance;
    public static IsoSortingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("(IsoSortingManager)");
                _instance = go.AddComponent<IsoSortingManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Constantes ───────────────────────────────────────────────────────

    // Cada banda de altura ocupa 2000 slots de sortingOrder
    // h0: 0~1999 | h1: 2000~3999 | h2: 4000~5999
    private const int HEIGHT_BAND = 2000;

    // Raio de culling: só sorteia sprites próximos à câmera
    private const float CULL_RANGE = 80f;

    // ─── Listas internas ──────────────────────────────────────────────────

    private static readonly List<IsoSortable> _staticList = new List<IsoSortable>(256);
    private static readonly List<IsoSortable> _movableList = new List<IsoSortable>(64);
    private static readonly List<IsoSortable> _belowAllList = new List<IsoSortable>(16);

    // Listas de trabalho (reusadas a cada frame, sem alloc)
    private static readonly List<IsoSortable> _visibleStatic = new List<IsoSortable>(128);
    private static readonly List<IsoSortable> _visibleMovable = new List<IsoSortable>(32);
    private static readonly List<IsoSortable> _sorted = new List<IsoSortable>(160);

    // ─── API Pública ──────────────────────────────────────────────────────

    public static void Register(IsoSortable s)
    {
        if (s.registered) return;

        if (s.renderBelowAll)
        {
            _belowAllList.Add(s);
        }
        else if (s.isMovable)
        {
            _movableList.Add(s);
        }
        else
        {
            _staticList.Add(s);
            BuildStaticDependencies(s);
        }

        s.registered = true;
    }

    public static void Unregister(IsoSortable s)
    {
        if (!s.registered) return;

        if (s.renderBelowAll) _belowAllList.Remove(s);
        else if (s.isMovable) _movableList.Remove(s);
        else
        {
            _staticList.Remove(s);
            RemoveStaticDependencies(s);
        }

        s.registered = false;
    }

    /// <summary>
    /// Força o recálculo de dependências estáticas de um objeto
    /// (use quando mudar logicalHeight ou footprint de um objeto estático em runtime).
    /// </summary>
    public static void RefreshStaticObject(IsoSortable s)
    {
        RemoveStaticDependencies(s);
        BuildStaticDependencies(s);
        s.forceSort = true;
    }

    // ─── Unity Loop ───────────────────────────────────────────────────────

    private void Update()
    {
        // 1. Atualiza cache dos móveis que se moveram
        RefreshMovableCache();

        // 2. Filtra visíveis por câmera
        FilterVisible(_staticList, _visibleStatic);
        FilterVisible(_movableList, _visibleMovable);

        // 3. Limpa e reconstrói dependências móveis
        ClearMovingDeps(_visibleStatic);
        ClearMovingDeps(_visibleMovable);
        BuildMovingDependencies(_visibleMovable, _visibleStatic);

        // 4. Topological sort
        _sorted.Clear();
        TopoSort(_visibleMovable, _visibleStatic, _sorted);

        // 5. Aplica sortingOrder
        ApplyOrders(_sorted);
        ApplyBelowAll(_belowAllList);
    }

    private void LateUpdate()
    {
        // Limpa flag hasChanged após o frame
        for (int i = 0; i < _movableList.Count; i++)
            _movableList[i].ClearChangedFlag();
    }

    // ─── Dependências Estáticas ───────────────────────────────────────────

    private static void BuildStaticDependencies(IsoSortable newSprite)
    {
        int count = _staticList.Count;
        for (int i = 0; i < count; i++)
        {
            IsoSortable other = _staticList[i];
            if (other == newSprite) continue;

            if (!newSprite.cachedBounds.Intersects(other.cachedBounds)) continue;

            int result = IsoSortable.Compare(newSprite, other);
            if (result == -1)
            {
                other.staticDeps.Add(newSprite);
                newSprite.inverseStaticDeps.Add(other);
            }
            else if (result == 1)
            {
                newSprite.staticDeps.Add(other);
                other.inverseStaticDeps.Add(newSprite);
            }
        }
    }

    private static void RemoveStaticDependencies(IsoSortable s)
    {
        for (int i = 0; i < s.inverseStaticDeps.Count; i++)
            s.inverseStaticDeps[i].staticDeps.Remove(s);

        s.staticDeps.Clear();
        s.inverseStaticDeps.Clear();
    }

    // ─── Dependências Móveis ──────────────────────────────────────────────

    private static void BuildMovingDependencies(List<IsoSortable> movables, List<IsoSortable> statics)
    {
        for (int i = 0; i < movables.Count; i++)
        {
            IsoSortable mover = movables[i];

            // Mover vs Statics
            for (int j = 0; j < statics.Count; j++)
            {
                IsoSortable stat = statics[j];
                if (!mover.cachedBounds.Intersects(stat.cachedBounds)) continue;

                int r = IsoSortable.Compare(mover, stat);

                // DEBUG TEMPORÁRIO - só loga quando é a Tree
                if (stat.name == "Tree")
                    Debug.Log($"[Compare] {mover.name}(pos={mover.worldPoint1}) vs Tree(p1={stat.worldPoint1} p2={stat.worldPoint2}) = {r} | +1=player frente | -1=tree frente");

                if (r == -1) stat.movingDeps.Add(mover);
                else if (r == 1) mover.movingDeps.Add(stat);
            }

            // Mover vs outros Movers
            for (int j = i + 1; j < movables.Count; j++)
            {
                IsoSortable other = movables[j];
                if (!mover.cachedBounds.Intersects(other.cachedBounds)) continue;

                int r = IsoSortable.Compare(mover, other);
                if (r == -1) other.movingDeps.Add(mover);
                else if (r == 1) mover.movingDeps.Add(other);
            }
        }
    }

    private static void ClearMovingDeps(List<IsoSortable> list)
    {
        for (int i = 0; i < list.Count; i++)
            list[i].movingDeps.Clear();
    }

    // ─── Cache ────────────────────────────────────────────────────────────

    private static void RefreshMovableCache()
    {
        for (int i = 0; i < _movableList.Count; i++)
        {
            IsoSortable s = _movableList[i];
            if (s.NeedsRefresh())
                s.RefreshCache();
        }
    }

    // ─── Visibilidade ─────────────────────────────────────────────────────

    private static void FilterVisible(List<IsoSortable> source, List<IsoSortable> dest)
    {
        dest.Clear();
        if (Camera.main == null) return;

        Vector2 cam = Camera.main.transform.position;

        for (int i = 0; i < source.Count; i++)
        {
            IsoSortable s = source[i];

            if (s.forceSort)
            {
                dest.Add(s);
                s.forceSort = false;
                continue;
            }

            Vector2 delta = s.worldPoint1 - cam;
            if (Mathf.Abs(delta.x) > CULL_RANGE || Mathf.Abs(delta.y) > CULL_RANGE)
                continue;

            dest.Add(s);
        }
    }

    // ─── Topological Sort ─────────────────────────────────────────────────

    private static readonly HashSet<int> _visited = new HashSet<int>();

    private static void TopoSort(List<IsoSortable> movables, List<IsoSortable> statics, List<IsoSortable> result)
    {
        _visited.Clear();

        // Visita móveis primeiro (ficam no topo quando empatados)
        for (int i = 0; i < movables.Count; i++)
            Visit(movables[i], result);

        for (int i = 0; i < statics.Count; i++)
            Visit(statics[i], result);
    }

    private static void Visit(IsoSortable node, List<IsoSortable> result)
    {
        int id = node.GetInstanceID();
        if (_visited.Contains(id)) return;
        _visited.Add(id);

        // Visita dependências primeiro (quem deve vir antes)
        for (int i = 0; i < node.movingDeps.Count; i++)
            Visit(node.movingDeps[i], result);

        for (int i = 0; i < node.staticDeps.Count; i++)
            Visit(node.staticDeps[i], result);

        result.Add(node);
    }

    // ─── Aplicação de Orders ──────────────────────────────────────────────

    // PPU do seu projeto
    private const float PIXELS_PER_UNIT = 32f;

    private static void ApplyOrders(List<IsoSortable> sorted)
    {
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].SortingOrder = i * 2;
        }
    }

    private static void ApplyBelowAll(List<IsoSortable> list)
    {
        int order = -list.Count * 2;
        for (int i = 0; i < list.Count; i++)
        {
            list[i].SortingOrder = order;
            order += 2;
        }
    }
}