using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using RimWorld.BaseGen;
using System.Security.Cryptography;
using UnityEngine;



namespace VFEArchitect
{


    [HarmonyPatch(typeof(Building_Door))]
    [HarmonyPatch("TicksToOpenNow", MethodType.Getter)]


    public static class VFEArchitect_Building_Door_TicksToOpenNow_Patch
    {


        [HarmonyPostfix]
        public static void NoDoorSpeed(Building_Door __instance, ref int __result)

        {

            if (__instance is Building_DoorSingle)
            {
                float num = 45f / 2.5f;
                num = ((!__instance.DoorPowerOn) ? (num * __instance.def.building.unpoweredDoorOpenSpeedFactor) : (num * (0.25f * __instance.def.building.poweredDoorOpenSpeedFactor)));
                __result= Mathf.RoundToInt(num);
            }
        }
    }


}