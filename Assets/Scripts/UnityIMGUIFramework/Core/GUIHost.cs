using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class GUIHost : MonoBehaviour
{
    private static readonly List<Action> DrawCallbacks = new List<Action>();
    private static Action[] callbackSnapshot = Array.Empty<Action>();
    private static GUIHost instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DrawCallbacks.Clear();
        callbackSnapshot = Array.Empty<Action>();
        instance = null;
    }

    public static void Register(Action drawCallback)
    {
        if (drawCallback == null)
        {
            throw new ArgumentNullException(nameof(drawCallback));
        }

        EnsureInstance();
        if (!DrawCallbacks.Contains(drawCallback))
        {
            DrawCallbacks.Add(drawCallback);
            callbackSnapshot = DrawCallbacks.ToArray();
        }
    }

    public static void Unregister(Action drawCallback)
    {
        if (drawCallback != null)
        {
            if (DrawCallbacks.Remove(drawCallback))
            {
                callbackSnapshot = DrawCallbacks.ToArray();
            }
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindFirstObjectByType<GUIHost>();
        if (instance != null)
        {
            return;
        }

        GameObject hostObject = new GameObject(nameof(GUIHost));
        DontDestroyOnLoad(hostObject);
        instance = hostObject.AddComponent<GUIHost>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        for (int i = 0; i < callbackSnapshot.Length; i++)
        {
            try
            {
                callbackSnapshot[i]?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
