using UnityEngine;

public static class IMGUIDrawing
{
    public static void Panel(Rect rect, GUIStyle style, Color tint)
    {
        Color previous = GUI.color;
        GUI.color = previous * tint;
        GUI.Box(rect, GUIContent.none, style);
        GUI.color = previous;
    }

    public static void ModalDim(Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(WindowLayout.ScreenRect(), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
