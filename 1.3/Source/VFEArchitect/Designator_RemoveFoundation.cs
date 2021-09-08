using RimWorld;
using UnityEngine;
using Verse;

namespace VFEArchitect
{
    public class Designator_RemoveFoundation : Designator_RemoveFloor
    {
        public Designator_RemoveFoundation()
        {
            defaultLabel = "VFEArchitect.DesignatorRemoveFoundation".Translate();
            defaultDesc = "VFEArchitect.DesignatorRemoveFoundationDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("ConcreteFoundation/RemoveFoundation_MenuIcon");
            hotKey = KeyBindingDefOf.Misc5;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c) => base.CanDesignateCell(c) && c.GetTerrain(Map).HasModExtension<TerrainExtension_Foundation>();
    }

    public class TerrainExtension_Foundation : DefModExtension
    {
    }
}