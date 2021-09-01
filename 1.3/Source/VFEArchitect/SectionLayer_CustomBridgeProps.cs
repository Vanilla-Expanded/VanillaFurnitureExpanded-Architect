using UnityEngine;
using Verse;

namespace VFEArchitect
{
    [StaticConstructorOnStartup]
    public class SectionLayer_CustomBridgeProps : SectionLayer
    {
        static SectionLayer_CustomBridgeProps()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var def in DefDatabase<TerrainDef>.AllDefs)
                    if (def.HasModExtension<TerrainExtension_CustomBridgeProps>())
                    {
                        var ext = def.GetModExtension<TerrainExtension_CustomBridgeProps>();
                        ext.rightMat = MaterialPool.MatFrom(ext.rightPath, ShaderDatabase.Transparent, def.color);
                        ext.loopMat = MaterialPool.MatFrom(ext.loopPath, ShaderDatabase.Transparent, def.color);
                    }
            });
        }

        public SectionLayer_CustomBridgeProps(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Terrain;

        public override void Regenerate()
        {
            ClearSubMeshes(MeshParts.All);
            var map = Map;
            var terrainGrid = map.terrainGrid;
            var cellRect = section.CellRect;
            var y = AltitudeLayer.TerrainScatter.AltitudeFor();
            foreach (var intVec in cellRect)
                if (ShouldDrawPropsBelow(intVec, terrainGrid))
                {
                    var c = intVec;
                    c.x++;
                    Material material;
                    var terrain = terrainGrid.TerrainAt(intVec);
                    if (c.InBounds(map) && terrain == terrainGrid.TerrainAt(c))
                        material = terrain.GetModExtension<TerrainExtension_CustomBridgeProps>().loopMat;
                    else
                        material = terrain.GetModExtension<TerrainExtension_CustomBridgeProps>().rightMat;

                    var subMesh = GetSubMesh(material);
                    var count = subMesh.verts.Count;
                    subMesh.verts.Add(new Vector3(intVec.x, y, intVec.z - 1));
                    subMesh.verts.Add(new Vector3(intVec.x, y, intVec.z));
                    subMesh.verts.Add(new Vector3(intVec.x + 1, y, intVec.z));
                    subMesh.verts.Add(new Vector3(intVec.x + 1, y, intVec.z - 1));
                    subMesh.uvs.Add(new Vector2(0f, 0f));
                    subMesh.uvs.Add(new Vector2(0f, 1f));
                    subMesh.uvs.Add(new Vector2(1f, 1f));
                    subMesh.uvs.Add(new Vector2(1f, 0f));
                    subMesh.tris.Add(count);
                    subMesh.tris.Add(count + 1);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count + 3);
                }

            FinalizeMesh(MeshParts.All);
        }

        private bool ShouldDrawPropsBelow(IntVec3 c, TerrainGrid terrGrid)
        {
            var terrain = terrGrid.TerrainAt(c);
            if (terrain == null || !terrain.bridge || !terrain.HasModExtension<TerrainExtension_CustomBridgeProps>()) return false;

            var c2 = c;
            c2.z--;
            if (!c2.InBounds(Map)) return false;

            var below = terrGrid.TerrainAt(c2);
            return !below.bridge && (below.passability == Traversability.Impassable || c2.SupportsStructureType(Map, terrain.terrainAffordanceNeeded));
        }
    }

    public class TerrainExtension_CustomBridgeProps : DefModExtension
    {
        public Material loopMat;
        public string loopPath;
        public Material rightMat;
        public string rightPath;
    }
}