using System.Collections;
using IMGUI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "INGAME";
    [SerializeField] private float minLoadingScreenTime = 2.0f;
    [SerializeField] private float readyDelay = 0.35f;

    private GUIImage backgroundImage;
    private GUIGroup loadingWindow;
    private GUILabel loadingLabel;
    private GUISlider loadingSlider;

    private GUIStyle windowStyle;
    private GUIStyle labelStyle;
    private GUIStyle sliderStyle;
    private GUIStyle thumbStyle;

    private bool loading;

    private readonly Color obsidian = new Color32(23, 25, 29, 255);
    private readonly Color ash = new Color32(58, 63, 70, 255);
    private readonly Color white = new Color32(247, 248, 250, 255);
    private readonly Color vitalGreen = new Color32(30, 200, 120, 255);

    public void StartGame()
    {
        if (loading)
            return;

        loading = true;

        GUIManager.instance?.SetBlocked(true);

        CreateLoadingUI();
        StartCoroutine(LoadGame());
    }

    private void CreateLoadingUI()
    {
        Texture2D bg = Resources.Load<Texture2D>("Textures/UI/LoadingBackground");

        if (bg != null)
        {
            backgroundImage = Widget.Image(
                bg,
                new Rect(0f, 0f, Screen.width, Screen.height),
                ScaleMode.StretchToFill
            );
            backgroundImage.Layer = 900;
        }

        windowStyle = GUIStyleMaker.Box(obsidian, white, 24, FontStyle.Bold)
            .Align(TextAnchor.UpperLeft)
            .Padding(22, 22, 18, 18);

        labelStyle = GUIStyleMaker.Label(white, 18, TextAnchor.MiddleCenter);

        sliderStyle = GUIStyleMaker.Slider(ash, 8f);

        thumbStyle = GUIStyleMaker.SliderThumb(
            vitalGreen,
            white,
            vitalGreen,
            18f,
            24f
        );

        Vector2 center = GUIManager.ScreenCenter;
        Vector2 windowSize = new Vector2(560f, 190f);
        Rect windowRect = CenterRect(center, windowSize);

        loadingWindow = Widget.Window(
            "LOADING",
            windowRect,
            "LoadingWindow",
            windowStyle
        );
        loadingWindow.Layer = 1000;

        loadingLabel = Widget.Label(
            loadingWindow,
            "불러오는 중... 0%",
            new Rect(
                windowRect.x + 40f,
                windowRect.y + 55f,
                windowRect.width - 80f,
                36f
            ),
            labelStyle
        );
        loadingLabel.Layer = 1001;

        loadingSlider = Widget.Slider(
            loadingWindow,
            new Rect(
                windowRect.x + 55f,
                windowRect.y + 120f,
                windowRect.width - 110f,
                24f
            ),
            0f,
            0f,
            1f,
            null,
            sliderStyle,
            thumbStyle
        );
        loadingSlider.Layer = 1001;
        loadingSlider.isInteractable = false;
    }

    private IEnumerator LoadGame()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

        if (operation == null)
        {
            Debug.LogError(
                $"[SceneLoad] 씬 '{targetSceneName}' 로드 실패. " +
                "Build Profiles에 추가했는지 확인."
            );

            loading = false;
            GUIManager.instance?.SetBlocked(false);

            yield break;
        }

        operation.allowSceneActivation = false;

        float startTime = Time.unscaledTime;
        float displayedProgress = 0f;

        while (true)
        {
            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float elapsed = Time.unscaledTime - startTime;
            float timeGate = Mathf.Clamp01(elapsed / minLoadingScreenTime);

            float targetVisual = Mathf.Min(realProgress, timeGate);

            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                targetVisual,
                Time.unscaledDeltaTime * 0.9f
            );

            if (loadingSlider != null)
                loadingSlider.Value = displayedProgress;

            if (loadingLabel != null)
                loadingLabel.Content.text =
                    $"불러오는 중... {Mathf.RoundToInt(displayedProgress * 100f)}%";

            bool loadReady = operation.progress >= 0.9f;
            bool timeReady = elapsed >= minLoadingScreenTime;
            bool barReady = displayedProgress >= 0.999f;

            if (loadReady && timeReady && barReady)
                break;

            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.Value = 1f;

        if (loadingLabel != null)
            loadingLabel.Content.text = "불러오는 중... 100%";

        yield return new WaitForSecondsRealtime(0.35f);

        operation.allowSceneActivation = true;
    }

    private static Rect CenterRect(Vector2 center, Vector2 size)
    {
        return new Rect(center - size * 0.5f, size);
    }
}