using System.Collections.Generic;
using UnityEngine;

namespace CrimsonSOS2
{
    // Frame-only geometry leaves the blue glass pixels and original UV mapping
    // untouched. Corner triangles stay inside the fixed-width end segments.
    internal static class CrimsonHullWindowFrame
    {
        internal static Color Tint { get { return new Color(0.80f,0.82f,0.83f,1f); } }

        internal static Vector2[] PixelTriangles()
        {
            List<Vector2> points = new List<Vector2>();
            float[] cuts = { 61f,310f,1010f,1162f,1862f,2111f };
            for (int i = 0; i < cuts.Length - 1; i++)
            {
                Quad(points,cuts[i],101f,cuts[i+1],258f);
                Quad(points,cuts[i],550f,cuts[i+1],639f);
            }
            Quad(points,61f,258f,184f,550f);
            Quad(points,1988f,258f,2111f,550f);
            Triangle(points,184f,336f,184f,258f,262f,258f);
            Triangle(points,1910f,258f,1988f,258f,1988f,336f);
            Triangle(points,184f,474f,260f,550f,184f,550f);
            Triangle(points,1988f,474f,1988f,550f,1912f,550f);
            return points.ToArray();
        }

        private static void Quad(List<Vector2> points, float left, float top, float right, float bottom)
        {
            Triangle(points,left,bottom,left,top,right,top);
            Triangle(points,left,bottom,right,top,right,bottom);
        }

        private static void Triangle(List<Vector2> points, float ax, float ay,
            float bx, float by, float cx, float cy)
        {
            points.Add(new Vector2(ax,ay)); points.Add(new Vector2(bx,by));
            points.Add(new Vector2(cx,cy));
        }

        internal static Vector3 Position(Vector2 pixel, int width)
        {
            float half = width * 0.5f;
            float[] cuts = { 61f,310f,1010f,1162f,1862f,2111f };
            float[] positions = { -half,-half+0.24f,-0.09f,0.09f,half-0.24f,half };
            int segment = 0;
            while (segment < 4 && pixel.x > cuts[segment+1]) segment++;
            float t = (pixel.x-cuts[segment])/(cuts[segment+1]-cuts[segment]);
            float x = positions[segment]+t*(positions[segment+1]-positions[segment]);
            return new Vector3(x,0f,0.5f-(pixel.y-101f)/538f);
        }

        internal static Vector2 Uv(Vector2 pixel)
        { return new Vector2(pixel.x/2172f,1f-pixel.y/724f); }
    }
}
