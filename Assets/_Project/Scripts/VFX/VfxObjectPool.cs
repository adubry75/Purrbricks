using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class VfxObjectPool<T> where T : Component
{
    private readonly string _rootName;
    private readonly Func<Transform, T> _factory;
    private readonly int _maxRetained;
    private readonly Stack<T> _available = new Stack<T>();
    private readonly List<T> _all = new List<T>();
    private Transform _root;

    public VfxObjectPool(string rootName, int maxRetained, Func<Transform, T> factory)
    {
        _rootName = rootName;
        _maxRetained = maxRetained;
        _factory = factory;
    }

    public T Get()
    {
        EnsureRoot();
        T item = null;
        while (_available.Count > 0 && item == null)
            item = _available.Pop();

        if (item == null)
        {
            item = _factory(_root);
            _all.Add(item);
        }

        item.transform.SetParent(_root, false);
        item.gameObject.SetActive(true);
        return item;
    }

    public void Release(T item)
    {
        if (item == null || !item.gameObject.activeSelf || !_all.Contains(item))
            return;

        if (_available.Count >= _maxRetained)
        {
            _all.Remove(item);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(item.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(item.gameObject);
            return;
        }

        item.gameObject.SetActive(false);
        if (_root != null)
            item.transform.SetParent(_root, false);
        _available.Push(item);
    }

    public void Clear()
    {
        _available.Clear();
        _all.Clear();
        if (_root == null)
            return;

        GameObject rootObject = _root.gameObject;
        _root = null;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(rootObject);
        else
            UnityEngine.Object.DestroyImmediate(rootObject);
    }

    private void EnsureRoot()
    {
        if (_root != null)
            return;

        _available.Clear();
        _all.RemoveAll(item => item == null);
        var rootObject = new GameObject(_rootName);
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            SceneManager.MoveGameObjectToScene(rootObject, activeScene);
        _root = rootObject.transform;
    }
}
