using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace VFEArchitect
{
    public class ArchitectMod : Mod
    {
        private static readonly AccessTools.FieldRef<Designator_Build, BuildableDef> desEntDef =
            AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");

        private static readonly AccessTools.FieldRef<Designator_Build, ThingDef> desStuffDef =
            AccessTools.FieldRefAccess<Designator_Build, ThingDef>("stuffDef");

        private static readonly AccessTools.FieldRef<Designator_Build, bool> desWriteStuff =
            AccessTools.FieldRefAccess<Designator_Build, bool>("writeStuff");

        private static readonly Action<Designator_Build, Event> desBaseProcessInput =
            AccessTools.Method(typeof(Designator_Build), "<>n__0").CreateDelegate<Action<Designator_Build, Event>>();

        private static readonly AccessTools.StructFieldRef<StatRequest, ThingDef> statReqStuff =
            AccessTools.StructFieldRefAccess<StatRequest, ThingDef>("stuffDefInt");

        private static readonly HashSet<ThingDef> costExtended = new HashSet<ThingDef>();
        private static readonly HashSet<BuildableDef> requireGodMode = new HashSet<BuildableDef>();
        private static readonly HashSet<ThingDef> prisonerProof = new HashSet<ThingDef>();
        internal static HashSet<TerrainDef> customBridges = new HashSet<TerrainDef>();
        private static readonly HashSet<TerrainDef> foundations = new HashSet<TerrainDef>();
        private static readonly HashSet<ThingDef> ignoreStuffFor = new HashSet<ThingDef>();
        private static readonly Dictionary<ThingDef, List<ThingDef>> extraStuffFor = new Dictionary<ThingDef, List<ThingDef>>();

        private static Harmony harm;

        public ArchitectMod(ModContentPack content) : base(content)
        {
            harm = new Harmony("vanillaexpanded.furniture.architect");
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.HasModExtension<StuffExtension_Cost>())
                    {
                        costExtended.Add(def);
                        var key = def.GetModExtension<StuffExtension_Cost>().thingDef;
                        if (!extraStuffFor.ContainsKey(key)) extraStuffFor.Add(key, new List<ThingDef>());
                        extraStuffFor[key].Add(def);
                    }

                    if (def.HasModExtension<BuildExtension_RequireGodMode>()) requireGodMode.Add(def);
                    if (def.HasModExtension<BuildingExtension_PrisonerProof>()) prisonerProof.Add(def);
                    if (def.HasModExtension<ThingExtension_IgnoreStuffFor>()) ignoreStuffFor.Add(def);
                }

                foreach (var def in DefDatabase<TerrainDef>.AllDefs)
                {
                    if (def.HasModExtension<TerrainExtension_CustomBridgeProps>()) customBridges.Add(def);
                    if (def.HasModExtension<TerrainExtension_Foundation>()) foundations.Add(def);
                    if (def.HasModExtension<BuildExtension_RequireGodMode>()) requireGodMode.Add(def);
                }

                if (costExtended.Any())
                {
                    harm.Patch(AccessTools.Method(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted),
                            new[] { typeof(BuildableDef), typeof(ThingDef), typeof(bool) }),
                        new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustStuff)));
                    harm.Patch(AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing)),
                        new HarmonyMethod(typeof(ArchitectMod), nameof(AdjustStuff2)));
                    harm.Patch(AccessTools.Method(typeof(Designator_Build), nameof(Designator_Build.ProcessInput)),
                        transpiler: new HarmonyMethod(typeof(ArchitectMod), nameof(ProcessInputTranspiler)));
                    if (ModLister.HasActiveModWithName("Replace Stuff"))
                    {
                        Log.Message("[Vanilla Furniture Expanded - Architect] Activating Replace Stuff compatibility patch");
                        var replaceFrame = AccessTools.TypeByName("Replace_Stuff.ReplaceFrame");
                        harm.Patch(AccessTools.Method(replaceFrame, "CountStuffHas"),
                            new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.CountStuffHas)));
                        harm.Patch(AccessTools.Method(replaceFrame, "TotalStuffNeeded", new[] { typeof(BuildableDef), typeof(ThingDef) }),
                            new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.TotalStuffNeeded)));
                        harm.Patch(AccessTools.Method(replaceFrame, "MaterialsNeeded"),
                            postfix: new HarmonyMethod(typeof(ReplaceStuffCompat), nameof(ReplaceStuffCompat.MaterialsNeeded)));
                    }
                }

                if (requireGodMode.Any())
                    harm.Patch(AccessTools.PropertyGetter(typeof(Designator_Build), nameof(Designator_Build.Visible)),
                        postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(RequireGodMode)));

                if (prisonerProof.Any())
                    harm.Patch(AccessTools.Method(typeof(Region), nameof(Region.Allows)),
                        transpiler: new HarmonyMethod(typeof(ArchitectMod), nameof(PrisonerProof)));

                if (customBridges.Any())
                    harm.Patch(AccessTools.Method(typeof(SectionLayer_BridgeProps), "ShouldDrawPropsBelow"),
                        postfix: new HarmonyMethod(typeof(ArchitectMod), nameof(IsVanillaBridge)));

                if (ignoreStuffFor.Any())
                {
                    harm.Patch(AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized)),
                        new HarmonyMethod(typeof(ArchitectMod), nameof(StatIgnoreStuff)));
                    harm.Patch(AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetExplanationFull)),
                        new HarmonyMethod(typeof(ArchitectMod), nameof(StatIgnoreStuff)));
                }
            });
        }

        public static void RequireGodMode(ref bool __result, Designator_Build __instance)
        {
            if (__result && requireGodMode.Contains(desEntDef(__instance)) && !DebugSettings.godMode) __result = false;
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

        public static IEnumerable<CodeInstruction> PrisonerProof(IEnumerable<CodeInstruction> instructions)
        {
            var found = false;
            var info = AccessTools.Field(typeof(TraverseParms), nameof(TraverseParms.canBashDoors));
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (!found && instruction.LoadsField(info))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.LoadField(typeof(TraverseParms), nameof(TraverseParms.pawn));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(typeof(Region), nameof(Region.door));
                    yield return CodeInstruction.Call(typeof(ArchitectMod), nameof(DontBash));
                    found = true;
                }
            }

            if (!found) Log.Error("[VFE - Architect] PrisonerProof transpiler failed");
        }

        public static bool DontBash(bool canBash, Pawn pawn, Building_Door door)
        {
            if (!canBash) return false;
            if (pawn.IsPrisoner && prisonerProof.Contains(door.def)) return false;
            return true;
        }

        public static void StatIgnoreStuff(ref StatRequest req, StatDef ___stat)
        {
            if (req.HasThing && req.StuffDef != null && ignoreStuffFor.Contains(req.Def) &&
                req.Def.GetModExtension<ThingExtension_IgnoreStuffFor>().stats.Contains(___stat))
                statReqStuff(ref req) = null;
        }

        public static IEnumerable<CodeInstruction> ProcessInputTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var info = AccessTools.Method(typeof(List<FloatMenuOption>), "Add");
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(info))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc, 2);
                    yield return new CodeInstruction(OpCodes.Ldloc, 4);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(ArchitectMod), nameof(AddExtras));
                }
            }
        }

        public static void AddExtras(List<FloatMenuOption> list, ThingDef stuff, Designator_Build des, Event ev)
        {
            if (extraStuffFor.ContainsKey(stuff) && !DebugSettings.godMode)
            {
                var thingDef = (ThingDef)desEntDef(des);
                foreach (var localStuffDef in extraStuffFor[stuff])
                {
                    string text;
                    if (des.sourcePrecept != null)
                        text = "ThingMadeOfStuffLabel".Translate(localStuffDef.LabelAsStuff, des.sourcePrecept.Label);
                    else
                        text = GenLabel.ThingLabel(thingDef, localStuffDef);

                    text = text.CapitalizeFirst();
                    list.Add(new FloatMenuOption(text, delegate
                    {
                        desBaseProcessInput(des, ev);
                        Find.DesignatorManager.Select(des);
                        desStuffDef(des) = localStuffDef;
                        desWriteStuff(des) = true;
                    }, localStuffDef)
                    {
                        tutorTag = "SelectStuff-" + thingDef.defName + "-" + localStuffDef.defName
                    });
                }
            }
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