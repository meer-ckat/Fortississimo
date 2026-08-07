using UnityEngine;

public sealed class AnimatedListingStyles
{
    public GUIStyle Panel { get; }
    public GUIStyle Title { get; }
    public GUIStyle Label { get; }
    public GUIStyle ChangedLabel { get; }
    public GUIStyle Value { get; }
    public GUIStyle Button { get; }
    public GUIStyle DialogMessage { get; }

    private AnimatedListingStyles(IMGUITheme theme)
    {
        Panel = new GUIStyle(GUI.skin.box)
        {
            padding = theme.panelPadding ?? new RectOffset()
        };

        Title = new GUIStyle(GUI.skin.label)
        {
            fontSize = theme.titleFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        Title.normal.textColor = theme.normalTextColor;

        Label = new GUIStyle(GUI.skin.label)
        {
            fontSize = theme.labelFontSize,
            alignment = TextAnchor.MiddleLeft
        };
        Label.normal.textColor = theme.normalTextColor;

        ChangedLabel = new GUIStyle(Label);
        ChangedLabel.normal.textColor = theme.changedTextColor;

        Value = new GUIStyle(GUI.skin.label)
        {
            fontSize = theme.valueFontSize,
            alignment = TextAnchor.MiddleRight
        };
        Value.normal.textColor = theme.normalTextColor;

        Button = new GUIStyle(GUI.skin.button)
        {
            fontSize = theme.buttonFontSize,
            alignment = TextAnchor.MiddleCenter
        };

        DialogMessage = new GUIStyle(GUI.skin.label)
        {
            fontSize = theme.dialogFontSize,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        DialogMessage.normal.textColor = theme.normalTextColor;
    }

    public static AnimatedListingStyles Create(IMGUITheme theme)
    {
        return new AnimatedListingStyles(theme.IsUsable ? theme : IMGUITheme.Default);
    }
}
