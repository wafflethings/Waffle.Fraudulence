using System;
using HarmonyLib;
using UnityEngine;

namespace Waffle.Fraudulence.Behaviours;

[HarmonyPatch]
[DefaultExecutionOrder(int.MinValue)]
public class WaffleFraudManager : MonoSingleton<WaffleFraudManager>
{
    public SpawnableObject[] TerminalEnemyInfo;
    private static SpawnableObject[] s_oldEnemyArray;

    private void Awake()
    {
        Patcher.TryPatch();
    }

    [HarmonyPatch(typeof(EnemyInfoPage), nameof(EnemyInfoPage.UpdateInfo)), HarmonyPrefix]
    private static void AddFraudEnemies(EnemyInfoPage __instance)
    {
        s_oldEnemyArray = __instance.objects.enemies;
        __instance.objects.enemies = __instance.objects.enemies.AddRangeToArray(Instance.TerminalEnemyInfo);
    }

    [HarmonyPatch(typeof(EnemyInfoPage), nameof(EnemyInfoPage.UpdateInfo)), HarmonyPostfix]
    private static void RemoveFraudEnemies(EnemyInfoPage __instance)
    {
        __instance.objects.enemies = s_oldEnemyArray;
    }
}
