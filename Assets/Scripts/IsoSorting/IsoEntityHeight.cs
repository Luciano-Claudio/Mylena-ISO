using UnityEngine;
using System;

[RequireComponent(typeof(IsoSortable))]
public class IsoEntityHeight : MonoBehaviour
{
    [Header("Config")]
    public int startingHeight = 0;

    [Header("Debug - Somente Leitura")]
    [SerializeField] private int _currentHeight = 0;

    private IsoSortable _sortable;

    public int CurrentHeight => _currentHeight;

    /// <summary>
    /// Dispara sempre que a altura muda.
    /// Parâmetros: (int oldHeight, int newHeight)
    /// </summary>
    public event Action<int, int> OnHeightChanged;

    private void Awake()
    {
        _sortable = GetComponent<IsoSortable>();
    }

    private void Start()
    {
        SetHeight(startingHeight);
    }

    public void Ascend() => SetHeight(_currentHeight + 1);
    public void Descend() => SetHeight(_currentHeight - 1);
    public void ForceHeight(int height) => SetHeight(height);

    private void SetHeight(int height)
    {
        int clamped = Mathf.Max(0, height);
        if (_currentHeight == clamped) return;

        int oldHeight = _currentHeight;
        _currentHeight = clamped;
        _sortable.logicalHeight = clamped;
        _sortable.forceSort = true;

        OnHeightChanged?.Invoke(oldHeight, clamped);

        Debug.Log($"[IsoEntityHeight] {gameObject.name} → h{clamped}");
    }
}