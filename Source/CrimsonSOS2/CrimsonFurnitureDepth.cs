using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Native wall corner fillers sit above the wall plane. The normal 2-degree
    // sprite tilt does not clear them. Raise the north edge while keeping the
    // south edge below foreground walls; do not lift the entire furniture layer.
    internal static class CrimsonFurnitureDepth
    {
        private const float Slope = 0.20f;
        private const float SeatNorthShift = 0.20f;
        private static readonly Dictionary<Material, Material> SolidMaterials = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> EmissionMaterials = new Dictionary<Material, Material>();
        private static bool IsSingleSeat(ThingDef definition)
        {
            return definition != null && definition.defName.StartsWith("Crimson_", System.StringComparison.Ordinal)
                && definition.defName != "Crimson_SpacerToilet"
                && definition.size.x == 1 && definition.size.z == 1
                && definition.building != null && definition.building.isSittable;
        }
        internal static Matrix4x4 Transform(Graphic graphic, Vector3 location, Rot4 rotation,
            Thing thing, float extraRotation)
        {
            Vector3 offset = graphic.DrawOffset(rotation);
            ThingDef definition = thing == null ? null : thing.def.entityDefToBuild as ThingDef ?? thing.def;
            bool singleSeat = IsSingleSeat(definition);
            if (singleSeat) offset.z += SeatNorthShift;
            Quaternion facing = graphic.QuatFromRot(rotation) * Quaternion.AngleAxis(extraRotation, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(location + offset, facing, Vector3.one);
            // Attachments already occupy the native wall overlay layer. Keep
            // that entire surface above its backing wall in every direction.
            if (thing != null && thing.def.building != null && thing.def.building.isAttachment)
                return matrix;
            float floorZ = thing == null ? location.z : GenThing.TrueCenter(thing).z;
            float slope = Slope;
            float seatLift = 0f;
            if (singleSeat)
            {
                // A tall backrest must clear the cabinet behind its floor cell.
                // Hinge the extra depth at the south/front edge: its feet keep
                // their previous depth, so foreground tables/walls still win.
                slope = 0.55f;
                Bounds bounds = graphic.MeshAt(rotation).bounds;
                Vector3 extent = bounds.extents;
                float south = matrix.MultiplyPoint3x4(bounds.center).z
                    - Mathf.Abs(matrix.m20) * extent.x - Mathf.Abs(matrix.m21) * extent.y
                    - Mathf.Abs(matrix.m22) * extent.z;
                seatLift = (slope - Slope) * (floorZ - south) - Slope * SeatNorthShift;
            }
            // Anchor depth at the occupied floor cell, including artwork whose
            // visible body has been raised north of its feet by a draw offset.
            matrix.m10 = slope * matrix.m20;
            matrix.m11 = 1f + slope * matrix.m21;
            matrix.m12 = slope * matrix.m22;
            float floorY = location.y;
            if (thing != null && thing.def.category == ThingCategory.Building
                && thing.def.altitudeLayer >= AltitudeLayer.Building
                && (thing.def.building == null || !thing.def.building.isAttachment))
                floorY = AltitudeLayer.Building.AltitudeFor() + location.y - GenThing.TrueCenter(thing).y;
            matrix.m13 = floorY + offset.y + seatLift + slope * (matrix.m23 - floorZ);
            return matrix;
        }

        internal static void Draw(Graphic graphic, Vector3 location, Rot4 rotation,
            ThingDef definition, Thing thing, float extraRotation)
        {
            if (thing == null && IsSingleSeat(definition)) location.z += SeatNorthShift;
            Mesh mesh = graphic.MeshAt(rotation);
            Matrix4x4 matrix = Transform(graphic, location, rotation, thing, extraRotation);
            Material material = graphic.MatAt(rotation, thing);
            Material solid = SolidMaterial(material);
            // Furniture housings always receive the room's light. The old
            // full-sprite post-light pass hid that response under an unlit copy.
            Graphics.DrawMesh(mesh, matrix, solid, 0);
            Material emission = EmissionMaterial(material, thing);
            if (emission != null)
            {
                matrix.m13 += .002f;
                Graphics.DrawMesh(mesh, matrix, emission, 0);
            }
            if (graphic.ShadowGraphic != null)
                graphic.ShadowGraphic.DrawWorker(location, rotation, definition, thing, extraRotation);
        }

        internal static void Print(SectionLayer layer, Thing thing, Graphic graphic,
            Vector2 size, float offsetZ = 0f)
        {
            Vector3 location = GenThing.TrueCenter(thing) + new Vector3(0f, 0f, offsetZ);
            Matrix4x4 matrix = Transform(graphic, location, thing.Rotation, thing, 0f);
            Material material = graphic.MatAt(thing.Rotation, thing);
            Material solid = SolidMaterial(material);
            PrintQuad(layer.GetSubMesh(solid), matrix, size);
            Material emission = EmissionMaterial(material, thing);
            if (emission != null)
            {
                matrix.m13 += .002f;
                PrintQuad(layer.GetSubMesh(emission), matrix, size);
            }
        }

        private static Material EmissionMaterial(Material material, Thing thing)
        {
            // Cutout graphics already own any separate light overlay. This
            // path only repairs legacy furniture using a whole post-light image.
            if (material.shader != ShaderDatabase.TransparentPostLight) return null;
            if (thing != null)
            {
                if (!(thing is Building) || thing.def.entityDefToBuild != null) return null;
                CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                CompFlickable flick = thing.TryGetComp<CompFlickable>();
                if (power == null || power.PowerNet == null || !power.PowerOn
                    || (flick != null && !flick.SwitchIsOn)) return null;
            }
            Material emission;
            if (!EmissionMaterials.TryGetValue(material, out emission))
            {
                // Reuse the cropped, signal-only mask extraction validated for
                // the command seat; no texture readback happens while ticking.
                emission = CrimsonDirectionalSeatGraphic.CreateSignal(material);
                EmissionMaterials.Add(material, emission);
            }
            return emission;
        }

        private static Material SolidMaterial(Material material)
        {
            if (material.shader != ShaderDatabase.TransparentPostLight) return material;
            Material solid;
            if (!SolidMaterials.TryGetValue(material, out solid))
            {
                solid = new Material(material);
                solid.shader = ShaderDatabase.Cutout;
                SolidMaterials.Add(material, solid);
            }
            return solid;
        }

        private static void PrintQuad(LayerSubMesh sub, Matrix4x4 matrix, Vector2 size)
        {
            int start = sub.verts.Count;
            float left = -size.x / 2f, right = size.x / 2f;
            float bottom = -size.y / 2f, top = size.y / 2f;
            AddPrintVertex(sub, matrix, left, bottom, 0f, 0f);
            AddPrintVertex(sub, matrix, left, top, 0f, 1f);
            AddPrintVertex(sub, matrix, right, top, 1f, 1f);
            AddPrintVertex(sub, matrix, right, bottom, 1f, 0f);
            sub.tris.Add(start);sub.tris.Add(start+1);sub.tris.Add(start+2);
            sub.tris.Add(start);sub.tris.Add(start+2);sub.tris.Add(start+3);
        }

        private static void AddPrintVertex(LayerSubMesh sub, Matrix4x4 matrix,
            float x, float z, float u, float v)
        {
            sub.verts.Add(matrix.MultiplyPoint3x4(new Vector3(x, 0f, z)));
            sub.uvs.Add(new Vector3(u, v, 0f));
            sub.colors.Add(new Color32(255, 255, 255, 255));
        }

        internal static Vector2 Size(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            return new Vector2(size.x, size.z);
        }

        internal static float OverlayAltitude(Thing thing)
        {
            Graphic graphic = thing.Graphic;
            Matrix4x4 matrix = Transform(graphic, thing.DrawPos, thing.Rotation, thing, 0f);
            Bounds bounds = graphic.MeshAt(thing.Rotation).bounds;
            Vector3 extent = bounds.extents;
            return matrix.MultiplyPoint3x4(bounds.center).y + Mathf.Abs(matrix.m10)*extent.x
                + Mathf.Abs(matrix.m11)*extent.y + Mathf.Abs(matrix.m12)*extent.z + 0.03658537f;
        }
    }
}
