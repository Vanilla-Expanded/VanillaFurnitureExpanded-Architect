using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VFEArchitect
{
    public class ArchitectMod : Mod
    {
        private static readonly AccessTools.FieldRef<Designator_Build, BuildableDef> desEntDef = AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");
        private static AccessTools.StructFieldRef<StatRequest, ThingDef> statReqStuff;
        private static readonly HashSet<ThingDef> costExtended = new HashSet<ThingDef>();
        private static readonly HashSet<BuildableDef> requireGodMode = new HashSet<BuildableDef>();
        private static readonly HashSet<ThingDef> prisonerProof = new HashSet<ThingDef>();
        internal static HashSet<TerrainDef> customBridges = new HashSet<TerrainDef>();
        private static readonly HashSet<TerrainDef> foundations = new HashSet<TerrainDef>();

        public ArchitectMod(ModContentPack content) : base(content)
        {
            var harm = new Harmony("vanillaexpanded.furniture.architect");
            harm.Patch(AccessTools.Method(typeof(SectionLayer_BridgeProps), "ShouldDrawPropsBelow"), postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(IsVanillaBridge)));
            harm.Patch(AccessTools.Method(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted), new[] {typeof(BuildableDef), typeof(ThingDef), typeof(bool)}),
                new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustStuff)));
            harm.Patch(AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing)), new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustStuff2)));
            harm.Patch(AccessTools.PropertyGetter(typeof(Designator_Build), nameof(Designator_Build.Visible)),
                postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(RequireGodMode)));
            harm.Patch(AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsOfDef)), new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustCount)));
            harm.Patch(AccessTools.Method(typeof(ResourceCounter), nameof(ResourceCounter.GetCount), new[] {typeof(ThingDef)}),
                new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustCount)));
            harm.Patch(AccessTools.Method(typeof(Region), nameof(Region.Allows)), new HarmonyMethod(typeof(ArchitectMod), nameof(PrisonerProof)));
            statReqStuff = AccessTools.StructFieldRefAccess<StatRequest, ThingDef>("stuffDefInt");
            harm.Patch(AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized)),
                new HarmonyMethod(typeof(ArchitectMod), nameof(StatIgnoreStuff)));
            harm.Patch(AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetExplanationFull)),
                new HarmonyMethod(typeof(ArchitectMod), nameof(StatIgnoreStuff)));
            if (ModLister.HasActiveModWithName("Replace Stuff"))
            {
                Log.Message("[Vanilla Furniture Expanded - Architect] Activating Replace Stuff compatibility patch");
                var replaceFrame = AccessTools.TypeByName("Replace_Stuff.ReplaceFrame");
                harm.Patch(AccessTools.Method(replaceFrame, "CountStuffHas"), new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.CountStuffHas)));
                harm.Patch(AccessTools.Method(replaceFrame, "TotalStuffNeeded", new[] {typeof(BuildableDef), typeof(ThingDef)}),
                    new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.TotalStuffNeeded)));
                harm.Patch(AccessTools.Method(replaceFrame, "MaterialsNeeded"), postfix: new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.MaterialsNeeded)));
            }

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.HasModExtension<StuffExtension_Cost>()) costExtended.Add(def);
                    if (def.HasModExtension<BuildExtension_RequireGodMode>()) requireGodMode.Add(def);
                    if (def.HasModExtension<BuildingExtension_PrisonerProof>()) prisonerProof.Add(def);
                }

                foreach (var def in DefDatabase<TerrainDef>.AllDefs)
                {
                    if (def.HasModExtension<TerrainExtension_CustomBridgeProps>()) customBridges.Add(def);
                    if (def.HasModExtension<TerrainExtension_Foundation>()) foundations.Add(def);
                    if (def.HasModExtension<BuildExtension_RequireGodMode>()) requireGodMode.Add(def);
                }
            });
        }

        public static void AdjustCount(ref ThingDef __0)
        {
            if (__0 != null && costExtended.Contains(__0)) __0 = __0.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void RequireGodMode(ref bool __result, Designator_Build __instance)
        {
            if (__result && requireGodMode.Contains(desEntDef.Invoke(__instance)) && !DebugSettings.godMode) __result = false;
        }

        public static void AdjustStuff(ref ThingDef stuff)
        {
            if (stuff != null && costExtended.Contains(stuff)) stuff = stuff.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void AdjustStuff2(ref ThingDef def)
        {
            AdjustStuff(ref def);
        }

        public static void IsVanillaBridge(IntVec3 c, TerrainGrid terrGrid, ref bool __result)
        {
            if (__result && customBridges.Contains(terrGrid.TerrainAt(c))) __result = false;
        }

        public static bool PrisonerProof(TraverseParms tp, Region __instance, ref bool __result)
        {
            if (tp.mode == TraverseMode.ByPawn && tp.pawn != null && __instance.door != null && tp.pawn.IsPrisoner && tp.pawn.InMentalState && tp.canBashDoors &&
                !__instance.door.CanPhysicallyPass(tp.pawn) && prisonerProof.Contains(__instance.door.def)) return __result = false;

            return true;
        }

        public static void StatIgnoreStuff(ref StatRequest req, StatDef ___stat)
        {
            if (req.HasThing && req.StuffDef != null && req.Def.HasModExtension<ThingExtension_IgnoreStuffFor>() &&
                req.Def.GetModExtension<ThingExtension_IgnoreStuffFor>().stats.Contains(___stat))
                statReqStuff.Invoke(ref req) = null;
        }

        public static class ReplaceStuffCompat
        {
            public static bool CountStuffHas(Frame __instance, ref int __result)
            {
                if (costExtended.Contains(__instance.Stuff))
                {
                    __result = __instance.resourceContainer.TotalStackCountOfDef(__instance.Stuff.GetModExtension<StuffExtension_Cost>().thingDef);
                    return false;
                }

                return true;
            }

            public static void TotalStuffNeeded(ref ThingDef stuff)
            {
                if (costExtended.Contains(stuff)) stuff = stuff.GetModExtension<StuffExtension_Cost>().thingDef;
            }

            public static void MaterialsNeeded(List<ThingDefCountClass> __result)
            {
                foreach (var countClass in __result.Where(countClass => costExtended.Contains(countClass.thingDef)))
                    countClass.thingDef = countClass.thingDef.GetModExtension<StuffExtension_Cost>().thingDef;
            }
        }
    }

    public class StuffExtension_Cost : DefModExtension
    {
        public ThingDef thingDef;
    }

    public class BuildExtension_RequireGodMode : DefModExtension
    {
    }

    public class BuildingExtension_PrisonerProof : DefModExtension
    {
    }

    public class ThingExtension_IgnoreStuffFor : DefModExtension
    {
        public List<StatDef> stats;
    }
}