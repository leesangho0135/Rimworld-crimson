using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Native book storage, reading, research benefits, contents UI and saving
    // remain owned by Building_Bookcase. Only the upright 3x4 display differs.
    [StaticConstructorOnStartup]
    public sealed class CrimsonBookshelf : Building_Bookcase
    {
        internal const int Columns = 4;
        internal const int Rows = 3;
        internal const int Capacity = Columns * Rows;
        internal const float BookWidth = 0.12f;
        internal const float BookHeight = 0.27f;
        private static readonly MethodInfo PreventDrawing = AccessTools.Method(typeof(ThingWithComps), "CompsPreventDrawing");
        private static readonly FieldInfo InnerContainer = AccessTools.Field(typeof(Building_Bookcase), "innerContainer");
        private static Mesh uprightBookMesh;
        private static Graphic crimsonSpine;
        private int bookshelfVersion;

        public override void PostMake()
        {
            base.PostMake();
            bookshelfVersion = 1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bookshelfVersion, "crimsonBookshelfVersion", 0);
            // The converted 1x1 cabinet used on-map medicine stacks rather
            // than a holder. A missing book holder must be initialized on load.
            if (Scribe.mode == LoadSaveMode.LoadingVars && InnerContainer.GetValue(this) == null)
                InnerContainer.SetValue(this, new ThingOwner<Book>(this, false, LookMode.Deep));
            if (Scribe.mode != LoadSaveMode.PostLoadInit || bookshelfVersion != 0) return;
            IStorageGroupMember member = this;
            if (member.Group != null) StorageGroupUtility.SetStorageGroup(member, null, true);
            GetStoreSettings().CopyFrom(def.building.defaultStorageSettings);
            Notify_SettingsChanged();
            bookshelfVersion = 1;
        }

        internal static Vector3 SlotOffset(int index)
        {
            int column = index % Columns;
            int row = index / Columns;
            // Keep each lower edge above its shelf board after shortening.
            return new Vector3((column - 1.5f) * 0.19f, 0.012f, -0.10f + row * 0.365f);
        }

        internal static Vector3[] BookVertices()
        {
            float left = -BookWidth * 0.5f, right = BookWidth * 0.5f;
            float bottom = -BookHeight * 0.5f;
            float top = BookHeight * 0.5f;
            return new[] {
                new Vector3(left, 0f, bottom), new Vector3(right, 0f, bottom),
                // A slight inward taper follows the cabinet's perspective;
                // spines remain standing upright, without exposing page tops.
                new Vector3(left * 0.94f, 0f, top), new Vector3(right * 0.94f, 0f, top) };
        }

        private static Mesh UprightBookMesh
        {
            get
            {
                if (uprightBookMesh != null) return uprightBookMesh;
                // Display a closed crimson spine only. The actual Book objects,
                // their colors, pages, contents and reading benefits are untouched.
                uprightBookMesh = new Mesh();
                uprightBookMesh.name = "CrimsonUprightBookSpine";
                uprightBookMesh.vertices = BookVertices();
                uprightBookMesh.uv = new[] {
                    new Vector2(508f/1254f, 52f/1254f), new Vector2(747f/1254f, 52f/1254f),
                    new Vector2(508f/1254f, 1201f/1254f), new Vector2(747f/1254f, 1201f/1254f) };
                uprightBookMesh.triangles = new[] { 0, 2, 3, 0, 3, 1 };
                uprightBookMesh.RecalculateNormals();
                uprightBookMesh.RecalculateBounds();
                uprightBookMesh.UploadMeshData(true);
                return uprightBookMesh;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!(bool)PreventDrawing.Invoke(this, null))
            {
                Graphic.Draw(drawLoc, Rotation, this);
                // The remapped north view exposes the open front to the south.
                // Both side panels and the actual rear conceal every book.
                if (Rotation == Rot4.North)
                {
                    if (crimsonSpine == null)
                        crimsonSpine = GraphicDatabase.Get<Graphic_Single>("Crimson/Decor/Extras/CrimsonBookSpineV111",
                            ShaderDatabase.Cutout, Vector2.one, Color.white);
                    IReadOnlyList<Book> books = HeldBooks;
                    for (int index = 0; index < books.Count && index < Capacity; index++)
                    {
                        Graphic display = crimsonSpine;
                        // Follow the same depth slope as the cabinet so books
                        // stay inside it while foreground walls still occlude it.
                        Matrix4x4 transform = CrimsonFurnitureDepth.Transform(display,
                            drawLoc + SlotOffset(index), Rot4.North, this, 0f);
                        Graphics.DrawMesh(UprightBookMesh, transform,
                            display.MatAt(Rot4.North, this), 0);
                    }
                }
            }
            Comps_DrawAt(drawLoc, flip);
            Comps_PostDraw();
        }
    }
}
