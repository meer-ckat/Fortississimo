using System;
using System.Collections.Generic;
using UnityEngine;

public static class GUIManager
{
    private sealed class GUIState
    {
        public Rect rect;
        public Vector2 startPosition;
        public Vector2 targetPosition;
        public double startTime;
        public float duration;
        public TweenMethod tweenMethod;
        public Action onComplete;
        public bool moving;
    }

    private static readonly Dictionary<string, GUIState> Registry =
        new Dictionary<string, GUIState>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Registry.Clear();
    }

    public static void Register(string id, Rect initialRect)
    {
        ValidateId(id);
        Registry[id] = new GUIState { rect = initialRect };
    }

    public static bool Contains(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && Registry.ContainsKey(id);
    }

    public static Rect GetRect(string id)
    {
        GUIState state = GetState(id);
        EvaluateMovement(state);
        return state.rect;
    }

    public static bool TryGetRect(string id, out Rect rect)
    {
        if (string.IsNullOrWhiteSpace(id) || !Registry.TryGetValue(id, out GUIState state))
        {
            rect = default;
            return false;
        }

        EvaluateMovement(state);
        rect = state.rect;
        return true;
    }

    public static void SetRect(string id, Rect rect)
    {
        GUIState state = GetState(id);
        state.moving = false;
        state.onComplete = null;
        state.rect = rect;
    }

    public static void MoveTo(
        string id,
        Vector2 targetPosition,
        float duration,
        TweenMethod tweenMethod,
        Action onComplete = null)
    {
        GUIState state = GetState(id);
        EvaluateMovement(state);

        state.startPosition = state.rect.position;
        state.targetPosition = targetPosition;
        state.startTime = Time.unscaledTimeAsDouble;
        state.duration = Mathf.Max(0f, duration);
        state.tweenMethod = tweenMethod;
        state.onComplete = onComplete;
        state.moving = true;

        EvaluateMovement(state);
    }

    public static bool IsMoving(string id)
    {
        GUIState state = GetState(id);
        EvaluateMovement(state);
        return state.moving;
    }

    public static void Stop(string id)
    {
        GUIState state = GetState(id);
        EvaluateMovement(state);
        state.moving = false;
        state.onComplete = null;
    }

    public static bool Remove(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && Registry.Remove(id);
    }

    public static void Clear()
    {
        Registry.Clear();
    }

    private static GUIState GetState(string id)
    {
        ValidateId(id);
        if (!Registry.TryGetValue(id, out GUIState state))
        {
            throw new InvalidOperationException($"No GUI rect is registered for '{id}'.");
        }

        return state;
    }

    private static void EvaluateMovement(GUIState state)
    {
        if (!state.moving)
        {
            return;
        }

        float progress = state.duration <= 0f
            ? 1f
            : Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - state.startTime) / state.duration));
        float eased = IMGUIEase.Evaluate(state.tweenMethod, progress);
        state.rect.position = Vector2.LerpUnclamped(state.startPosition, state.targetPosition, eased);

        if (progress < 1f)
        {
            return;
        }

        state.rect.position = state.targetPosition;
        state.moving = false;
        Action completion = state.onComplete;
        state.onComplete = null;
        completion?.Invoke();
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A non-empty GUI id is required.", nameof(id));
        }
    }
}
