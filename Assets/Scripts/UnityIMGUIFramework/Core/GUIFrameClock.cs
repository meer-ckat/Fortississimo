using UnityEngine;

public static class GUIFrameClock
{
    private static int capturedFrame = -1;
    private static double capturedTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        capturedFrame = -1;
        capturedTime = 0d;
    }

    public static double Capture()
    {
        if (capturedFrame != Time.frameCount)
        {
            capturedFrame = Time.frameCount;
            capturedTime = Time.unscaledTimeAsDouble;
        }

        return capturedTime;
    }
}
