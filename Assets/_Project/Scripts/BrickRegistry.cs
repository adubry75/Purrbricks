using System.Collections.Generic;
using UnityEngine;

public class BrickRegistry : MonoBehaviour
{
    public static BrickRegistry Instance { get; private set; }

    [SerializeField] private BrickDefinition[] _definitions;

    private Dictionary<char, BrickDefinition> _map;

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
        _map = new Dictionary<char, BrickDefinition>();

        if (_definitions == null) return;

        foreach (var def in _definitions)
        {
            if (def == null) continue;

            char key = def.symbol;
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning($"Duplicate BrickDefinition symbol '{key}' in registry. Using the first one.");
                continue;
            }

            _map.Add(key, def);
        }
    }

    public BrickDefinition Get(char symbol)
    {
        if (_map == null) BuildMap();
        if (_map != null && _map.TryGetValue(symbol, out var def)) return def;
        return null;
    }
}
