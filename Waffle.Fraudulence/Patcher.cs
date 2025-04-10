using HarmonyLib;
using UnityEngine;

namespace Waffle.Fraudulence;

public static class Patcher
{
    private const string HarmonyId = "waffle.ultrakill.fraud";

    public static void TryPatch()
    {
        if (!Harmony.HasAnyPatches(HarmonyId))
        {
            Debug.LogWarning("Trying to patch Fraud!, patch code run.");
            new Harmony(HarmonyId).PatchAll();
        }
    }
}
