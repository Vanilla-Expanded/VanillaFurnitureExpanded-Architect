using RimWorld;
using UnityEngine;
using Verse;

namespace VFEArchitect
{
    public class Building_DoorSingle : Building_Door
    {
        public override void Draw()
        {
            Rotation = DoorRotationAt(Position, Map);
            var d = 0f + 0.9f * Mathf.Clamp01(ticksSinceOpen / (float) TicksToOpenNow);
            var vector = new Vector3(0f, 0f, -1f);
            var mesh = MeshPool.plane10;
            var rotation = Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            vector = rotation.AsQuat * vector;
            var vector2 = DrawPos;
            vector2.y = AltitudeLayer.DoorMoveable.AltitudeFor();
            vector2 += vector * d;
            Graphics.DrawMesh(mesh, vector2, Rotation.AsQuat, Graphic.MatAt(Rotation), 0);
            Graphic.ShadowGraphic?.DrawWorker(vector2, Rotation, def, this, 0f);
            Comps_PostDraw();
        }
    }
}