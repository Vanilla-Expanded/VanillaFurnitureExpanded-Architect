using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VFEArchitect
{
    public class ArchitectMod : Mod
    {
        private static readonly AccessTools.FieldRef<Designator_Build, BuildableDef> desEntDef = AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");
        private static AccessTools.StructFieldRef<StatRequest, ThingDef> statReqStuff;

        public ArchitectMod(ModContentPack content) : base(content)
        {
            var harm = new Harmony("vanillaexpanded.furniture.architect");
            harm.Patch(AccessTools.Method(typeof(SectionLayer_BridgeProps), "ShouldDrawPropsBelow"), postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(IsVanillaBridge)));
            harm.Patch(AccessTools.Method(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted), new[] {typeof(BuildableDef), typeof(ThingDef), typeof(bool)}),
                new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustStuff)));
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
        }

        public static void AdjustCount(ref ThingDef __0)
        {
            if (__0 != null && __0.HasModExtension<StuffExtension_Cost>()) __0 = __0.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void RequireGodMode(ref bool __result, Designator_Build __instance)
        {
            if (__result && desEntDef.Invoke(__instance).HasModExtension<BuildExtension_RequireGodMode>() && !DebugSettings.godMode) __result = false;
        }

        public static void AdjustStuff(ref ThingDef stuff)
        {
            if (stuff != null && stuff.HasModExtension<StuffExtension_Cost>()) stuff = stuff.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void IsVanillaBridge(IntVec3 c, TerrainGrid terrGrid, ref bool __result)
        {
            if (__result && terrGrid.TerrainAt(c).HasModExtension<TerrainExtension_CustomBridgeProps>()) __result = false;
        }

        public static bool PrisonerProof(TraverseParms tp, Region __instance, ref bool __result)
        {
            if (tp.mode == TraverseMode.ByPawn && tp.pawn != null && __instance.door != null && tp.pawn.IsPrisoner && tp.pawn.InMentalState && tp.canBashDoors &&
                !__instance.door.CanPhysicallyPass(tp.pawn) && __instance.door.def.HasModExtension<BuildingExtension_PrisonerProof>()) return __result = false;

            return true;
        }

        public static void StatIgnoreStuff(ref StatRequest req, StatDef ___stat)
        {
            if (req.HasThing && req.StuffDef != null && req.Def.HasModExtension<ThingExtension_IgnoreStuffFor>() &&
                req.Def.GetModExtension<ThingExtension_IgnoreStuffFor>().stats.Contains(___stat))
                statReqStuff.Invoke(ref req) = null;
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