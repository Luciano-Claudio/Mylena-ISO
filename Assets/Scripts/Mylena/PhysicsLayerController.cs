using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gerencia automaticamente a Physics Layer do personagem
/// baseado na altura lógica. Inscrito no evento OnHeightChanged.
/// 
/// Convenção de layers:
///   Entities_H0, Entities_H1, Entities_H2...
/// 
/// Adicione no objeto PAI do personagem.
/// Arraste os colliders filhos que precisam mudar de layer em "managedColliders".
/// </summary>
public class PhysicsLayerController : MonoBehaviour
{
    [Header("Colliders que terão a layer alterada")]
    [Tooltip("Arraste aqui os GameObjects filhos que possuem Collider2D (corpo, trigger, etc).")]
    [SerializeField] private List<GameObject> managedObjects;

    [Header("Config")]
    [Tooltip("Prefixo da layer. Ex: 'Entities' → layers 'Entities_H0', 'Entities_H1'...")]
    [SerializeField] private string layerPrefix = "Entities";

    private IsoEntityHeight _heightEntity;

    private void Awake()
    {
        managedObjects = GetAllChildren(this.gameObject);
        _heightEntity = GetComponent<IsoEntityHeight>();

        if (_heightEntity == null)
        {
            Debug.LogError("[PhysicsLayerController] IsoEntityHeight não encontrado!", this);
            return;
        }

        _heightEntity.OnHeightChanged += HandleHeightChanged;
    }

    private void OnDestroy()
    {
        if (_heightEntity != null)
            _heightEntity.OnHeightChanged -= HandleHeightChanged;
    }

    private void HandleHeightChanged(int oldHeight, int newHeight)
    {
        string layerName = $"{layerPrefix}_H{newHeight}";
        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex == -1)
        {
            Debug.LogError($"[PhysicsLayerController] Layer '{layerName}' não existe! " +
                           $"Crie-a em Edit → Project Settings → Tags and Layers.", this);
            return;
        }

        foreach (var obj in managedObjects)
        {
            if (obj != null)
                obj.layer = layerIndex;
        }
    }
    private List<GameObject> GetAllChildren(GameObject parent)
    {
        // Get all transforms in the hierarchy, including the parent
        Transform[] allTransforms = parent.GetComponentsInChildren<Transform>(true);

        // Use LINQ to filter out the parent's transform and get the GameObjects as a List
        List<GameObject> childrenList = allTransforms
            .Where(t => t != parent.transform)
            .Select(t => t.gameObject)
            .ToList();

        childrenList.Add(parent); // Adiciona o próprio objeto pai à lista

        return childrenList;
    }
}