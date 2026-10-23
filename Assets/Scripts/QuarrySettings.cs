using UnityEngine;

public static class QuarrySettings
{
    public const string MasterVolumeKey = "Settings_MasterVolumePercent";
    public const string ReducedFlashKey = "Settings_ReducedFlash";
    public const string ShowSplitsKey = "Settings_ShowSpeedrunSplits";

    public static int MasterVolumePercent
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(MasterVolumeKey, 100), 0, 100);
        set
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            PlayerPrefs.SetInt(MasterVolumeKey, clamped);
            PlayerPrefs.Save();
            AudioListener.volume = clamped / 100f;
        }
    }

    public static bool ReducedHitFlash
    {
        get => PlayerPrefs.GetInt(ReducedFlashKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ReducedFlashKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ShowSpeedrunSplits
    {
        get => PlayerPrefs.GetInt(ShowSplitsKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ShowSplitsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void CycleVolumeStep()
    {
        int current = MasterVolumePercent;
        int next = current >= 100 ? 0 : current + 25;
        MasterVolumePercent = next;
    }

    public static void ApplyRuntimeAudio()
    {
        AudioListener.volume = MasterVolumePercent / 100f;
    }
}
