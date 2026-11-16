using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // A recessed octagonal light follows the inner plinth rim, below the floating body.
    internal static class CrimsonHoverRim
    {
        private static readonly Vector2[] edge = {
            new Vector2(-.74f,.73f),new Vector2(.74f,.73f),new Vector2(.85f,.55f),new Vector2(.85f,-.48f),
            new Vector2(.74f,-.65f),new Vector2(-.74f,-.65f),new Vector2(-.85f,-.48f),new Vector2(-.85f,.55f) };
        private static readonly Mesh[] meshes = { Ring(.10f), Ring(.18f) };
        private static readonly Material[] materials = { Light(.12f), Light(.035f) };
        private static Material Light(float alpha)
        {
            Material m=new Material(ShaderDatabase.TransparentPostLight);
            Texture2D glow=new Texture2D(2,32,TextureFormat.RGBA32,false);
            glow.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<32;y++)
            {
                float fade=Mathf.Sin(Mathf.PI*y/31f);
                // Attenuate RGB as well: the post-light pass must never retain a full-red core.
                float intensity=alpha*fade*fade;
                Color color=new Color(intensity,.025f*intensity,.06f*intensity,intensity);
                glow.SetPixel(0,y,color);glow.SetPixel(1,y,color);
            }
            glow.Apply(false,true);m.mainTexture=glow;m.color=Color.white;return m;
        }
        private static Mesh Ring(float width)
        {
            Vector3[] vertices=new Vector3[16];Vector2[] uv=new Vector2[16];int[] triangles=new int[48];
            for(int i=0;i<8;i++)
            {
                Vector2 inner=edge[i]*(1f-width);
                vertices[i*2]=new Vector3(edge[i].x,0,edge[i].y);vertices[i*2+1]=new Vector3(inner.x,0,inner.y);
                uv[i*2]=new Vector2(.5f,0f);uv[i*2+1]=new Vector2(.5f,1f);
                int j=(i+1)%8,k=i*6;triangles[k]=i*2;triangles[k+1]=j*2;triangles[k+2]=i*2+1;
                triangles[k+3]=j*2;triangles[k+4]=j*2+1;triangles[k+5]=i*2+1;
            }
            Mesh mesh=new Mesh();mesh.name="Crimson recessed plinth light";mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        private static bool Lit(Thing thing)
        { return thing is Building && thing.def.entityDefToBuild==null && CrimsonVisualState.Powered(thing); }
        internal static void Draw(Graphic pedestal,Vector3 loc,Thing thing)
        {
            if(!Lit(thing))return;
            // Above the plinth surface, below the sculpture's separate depth plane.
            Matrix4x4 matrix=CrimsonFurnitureDepth.Transform(pedestal,loc,Rot4.North,thing,0f);matrix.m13+=.00025f;
            for(int i=meshes.Length-1;i>=0;i--)Graphics.DrawMesh(meshes[i],matrix,materials[i],0);
        }
        internal static void Print(SectionLayer layer,Graphic pedestal,Thing thing)
        {
            if(!Lit(thing))return;
            Matrix4x4 matrix=CrimsonFurnitureDepth.Transform(pedestal,RimWorld.GenThing.TrueCenter(thing),Rot4.North,thing,0f);matrix.m13+=.00025f;
            for(int i=meshes.Length-1;i>=0;i--)
            {
                LayerSubMesh target=layer.GetSubMesh(materials[i]);int start=target.verts.Count;
                foreach(Vector3 v in meshes[i].vertices)target.verts.Add(matrix.MultiplyPoint3x4(v));
                foreach(Vector2 uv in meshes[i].uv)target.uvs.Add(new Vector3(uv.x,uv.y,0));
                for(int v=0;v<16;v++)target.colors.Add(new Color32(255,255,255,255));
                foreach(int index in meshes[i].triangles)target.tris.Add(start+index);
            }
        }
    }
}
