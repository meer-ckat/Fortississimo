using System;
using UnityEngine;

public readonly struct GUIStateScope : IDisposable
{
    private readonly Color previousColor;
    private readonly bool previousEnabled;

    public GUIStateScope(float alpha, bool enabled)
    {
        previousColor = GUI.color;
        previousEnabled = GUI.enabled;

        Color color = previousColor;
        color.a *= Mathf.Clamp01(alpha);
        GUI.color = color;
        GUI.enabled = previousEnabled && enabled;
    }

    public void Dispose()
    {
        GUI.color = previousColor;
        GUI.enabled = previousEnabled;
    }
}
