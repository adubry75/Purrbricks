using System.Collections.Generic;
using UnityEngine;

public class BrickTemplateRegistry : MonoBehaviour
{
    public static BrickTemplateRegistry Instance { get; private set; }

    [SerializeField] private BrickTemplate[] _templates;

    private Dictionary<string, BrickTemplate> _map;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildMap();
    }

    private void BuildMap()
    {
        _map = new Dictionary<string, BrickTemplate>();

        if (_templates == null) return;

        foreach (var t in _templates)
        {
            if (t == null || string.IsNullOrEmpty(t.id)) continue;

            if (_map.ContainsKey(t.id))
            {
                Debug.LogWarning($"Duplicate BrickTemplate id '{t.id}' in registry. Using first.");
                continue;
            }

            _map.Add(t.id, t);
        }
    }

    public BrickTemplate Get(string id)
    {
        if (_map == null) BuildMap();
        if (!string.IsNullOrEmpty(id) && _map != null && _map.TryGetValue(id, out var t)) return t;
        return null;
    }
}
