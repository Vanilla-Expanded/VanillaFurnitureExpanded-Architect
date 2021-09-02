using RimWorld;
using UnityEngine;
using Verse;

namespace VFEArchitect
{
    public class Designator_RemoveFoundation : Designator_RemoveBridge
    {
        public Designator_RemoveFoundation()
        {
            defaultLabel = "VFEArchitect.DesignatorRemoveFoundation".Translate();
            defaultDesc = "VFEArchitect.DesignatorRemoveFoundationDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/RemoveBridge");
            hotKey = KeyBindingDefOf.Misc5;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (c.InBounds(Map) && !c.GetTerrain(Map).HasModExtension<TerrainExtension_Foundation>()) return false;

            return base.CanDesignateCell(c);
        }
    }

    public class TerrainExtension_Foundation : DefModExtension
    {
    }
}