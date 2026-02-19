using System.Collections.Generic;
using UnityEngine;

public class BrickSkinRegistry : MonoBehaviour
{
    public static BrickSkinRegistry Instance { get; private set; }

    [SerializeField] private BrickSkin[] _skins;

    private Dictionary<string, BrickSkin> _map;

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
        _map = new Dictionary<string, BrickSkin>();

        if (_skins == null) return;

        foreach (var s in _skins)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;

            if (_map.ContainsKey(s.id))
            {
                Debug.LogWarning($"Duplicate BrickSkin id '{s.id}' in registry. Using first.");
                continue;
            }

            _map.Add(s.id, s);
        }
    }

    public BrickSkin Get(string id)
    {
        if (_map == null) BuildMap();
        if (!string.IsNullOrEmpty(id) && _map != null && _map.TryGetValue(id, out var s)) return s;
        return null;
    }
}
