using System;
using HarmonyLib;
using UnityEngine;

namespace Waffle.Fraudulence.Behaviours.Enemies.Cultist;

[HarmonyPatch]
public class CultistBeam : MonoBehaviour
{
    [HarmonyPatch(typeof(RevolverBeam), nameof(RevolverBeam.Shoot)), HarmonyPrefix]
    private static void MakeCultBeamsPassThrough(RevolverBeam __instance)
    {
        try
        {
            if (__instance?.GetComponent<CultistBeam>() == null)
            {
                Debug.Log("Not cult beam!");
                return;
            }

            __instance.ignoreEnemyTrigger = __instance.enemyLayerMask;
            __instance.pierceLayerMask = __instance.enemyLayerMask;
        }
        catch (Exception ex)
        {
            Debug.LogError("Fraud cultistbeam error! Catching to prevent breaking everything.\n\n" + ex);
        }
    }
}
