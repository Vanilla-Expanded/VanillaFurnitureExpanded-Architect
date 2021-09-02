using HarmonyLib;
using RimWorld;
using Verse;

namespace VFEArchitect
{
    public class ArchitectMod : Mod
    {
        public static AccessTools.FieldRef<Designator_Build, BuildableDef> GetEntDef = AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");

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
        }

        public static void AdjustCount(ref ThingDef __0)
        {
            if (__0 != null && __0.HasModExtension<StuffExtension_Cost>()) __0 = __0.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void RequireGodMode(ref bool __result, Designator_Build __instance)
        {
            if (__result && GetEntDef.Invoke(__instance).HasModExtension<BuildExtension_RequireGodMode>() && !DebugSettings.godMode) __result = false;
        }

        public static void AdjustStuff(ref ThingDef stuff)
        {
            if (stuff != null && stuff.HasModExtension<StuffExtension_Cost>()) stuff = stuff.GetModExtension<StuffExtension_Cost>().thingDef;
        }

        public static void IsVanillaBridge(IntVec3 c, TerrainGrid terrGrid, ref bool __result)
        {
            if (__result && terrGrid.TerrainAt(c).HasModExtension<TerrainExtension_CustomBridgeProps>()) __result = false;
        }
    }

    public class StuffExtension_Cost : DefModExtension
    {
        public ThingDef thingDef;
    }

    public class BuildExtension_RequireGodMode : DefModExtension
    {
    }
}