using UnityEngine;

public enum TweenMethod
{
    Linear,
    InCubic,
    OutCubic,
    InOutCubic,
    InSine,
    OutSine,
    InBack,
    OutBack
}

public static class IMGUIEase
{
    private const float BackOvershoot = 1.70158f;

    public static float Evaluate(TweenMethod method, float t)
    {
        t = Mathf.Clamp01(t);

        switch (method)
        {
            case TweenMethod.InCubic:
                return t * t * t;
            case TweenMethod.OutCubic:
            {
                float inverse = 1f - t;
                return 1f - inverse * inverse * inverse;
            }
            case TweenMethod.InOutCubic:
                return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            case TweenMethod.InSine:
                return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
            case TweenMethod.OutSine:
                return Mathf.Sin(t * Mathf.PI * 0.5f);
            case TweenMethod.InBack:
            {
                float coefficient = BackOvershoot + 1f;
                return coefficient * t * t * t - BackOvershoot * t * t;
            }
            case TweenMethod.OutBack:
            {
                float shifted = t - 1f;
                float coefficient = BackOvershoot + 1f;
                return 1f + coefficient * shifted * shifted * shifted
                    + BackOvershoot * shifted * shifted;
            }
            default:
                return t;
        }
    }
}
