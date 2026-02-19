using System.Collections.Generic;
using UnityEngine;

public class PowerupRegistry : MonoBehaviour
{
    public static PowerupRegistry Instance { get; private set; }

    [SerializeField] private PowerupDefinition[] _powerups;

    private Dictionary<string, PowerupDefinition> _map;

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
        _map = new Dictionary<string, PowerupDefinition>();

        if (_powerups == null) return;

        foreach (var p in _powerups)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;

            if (_map.ContainsKey(p.id))
            {
                Debug.LogWarning($"Duplicate PowerupDefinition id '{p.id}' in registry. Using first.");
                continue;
            }

            _map.Add(p.id, p);
        }
    }

    public PowerupDefinition Get(string id)
    {
        if (_map == null) BuildMap();
        if (!string.IsNullOrEmpty(id) && _map != null && _map.TryGetValue(id, out var p)) return p;
        return null;
    }
}
