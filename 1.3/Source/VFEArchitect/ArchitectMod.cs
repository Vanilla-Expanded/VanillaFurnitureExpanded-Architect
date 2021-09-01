using HarmonyLib;
using RimWorld;
using Verse;

namespace VFEArchitect
{
    public class ArchitectMod : Mod
    {
        public ArchitectMod(ModContentPack content) : base(content)
        {
            var harm = new Harmony("vanillaexpanded.furniture.architect");
            harm.Patch(AccessTools.Method(typeof(SectionLayer_BridgeProps), "ShouldDrawPropsBelow"), postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(IsVanillaBridge)));
        }

        public static void IsVanillaBridge(IntVec3 c, TerrainGrid terrGrid, ref bool __result)
        {
            if (__result && terrGrid.TerrainAt(c).HasModExtension<TerrainExtension_CustomBridgeProps>()) __result = false;
        }
    }
}