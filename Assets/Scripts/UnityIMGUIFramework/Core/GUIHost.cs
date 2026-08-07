using UnityEngine;

/// <summary>
/// The single OnGUI entry point. Owns nothing about windows beyond driving the
/// stack once per event and lifting Escape out of any individual window's hands.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class GUIHost : MonoBehaviour
{
    private static GUIHost instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static void EnsureInstance()
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
        Event current = Event.current;

        // Escape is a stack decision: it belongs to whichever window is on top,
        // and no window below should also see it.
        if (current.type == UnityEngine.EventType.KeyDown &&
            current.keyCode == KeyCode.Escape &&
            GUIWindowStack.NotifyCancelPressed())
        {
            current.Use();
            return;
        }

        double guiTime = GUIFrameClock.Capture();
        bool advanceLifecycle = current.type == UnityEngine.EventType.Repaint;

        GUIWindowStack.DrawAll(guiTime, advanceLifecycle);
    }
}
