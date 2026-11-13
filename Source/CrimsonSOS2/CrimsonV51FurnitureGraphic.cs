using UnityEngine;
using Verse;
using RimWorld;

namespace CrimsonSOS2
{
    internal sealed class CrimsonV51ArtSpec
    {
        internal readonly bool powered;
        internal readonly Rect[] windows;
        internal readonly Vector2[] sizes;
        internal readonly float[] offsets;
        internal readonly bool nativeFrameTextures;
        internal CrimsonV51ArtSpec(bool powered, Rect[] windows, Vector2[] sizes, float[] offsets,
            bool nativeFrameTextures = false)
        { this.powered=powered; this.windows=windows; this.sizes=sizes; this.offsets=offsets; this.nativeFrameTextures=nativeFrameTextures; }
    }

    public sealed class CrimsonV51FurnitureGraphic : Graphic_Single
    {
        private CrimsonV51ArtSpec art;
        private Material[] frames;
        public override bool ShouldDrawRotated { get { return false; } }
        private Material Preview(int direction) { return frames==null ? mat : frames[direction+(art.powered?4:0)]; }
        public override Material MatSingle { get { return Preview(2); } }
        public override Material MatNorth { get { return Preview(0); } }
        public override Material MatEast { get { return Preview(1); } }
        public override Material MatSouth { get { return MatSingle; } }
        public override Material MatWest { get { return Preview(3); } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);art=CrimsonV51ArtRegistration.Find(path);
            frames=new Material[art.windows.Length];
            for(int index=0;index<frames.Length;index++)
            {
                Rect uv=art.windows[index];Material frame=new Material(mat);
                frame.mainTextureScale=Vector2.Scale(mat.mainTextureScale,new Vector2(uv.width,uv.height));
                frame.mainTextureOffset=mat.mainTextureOffset+Vector2.Scale(mat.mainTextureScale,new Vector2(uv.x,uv.y));
                if(art.nativeFrameTextures)
                {
                    frame.mainTexture=NativeFrame(mat.mainTexture,frame.mainTextureScale,frame.mainTextureOffset,index);
                    frame.mainTextureScale=Vector2.one;frame.mainTextureOffset=Vector2.zero;
                }
                frames[index]=frame;
            }
            if(art.nativeFrameTextures)mat=frames[art.powered?6:2];
        }
        internal static Texture2D NativeFrame(Texture source,Vector2 scale,Vector2 offset,int index)
        {
            // Some native ghost/blueprint shaders ignore material atlas tiling.
            // Opted-in art gets an in-memory frame, keeping source PNGs intact.
            int width=Mathf.Max(1,Mathf.RoundToInt(source.width*scale.x));
            int height=Mathf.Max(1,Mathf.RoundToInt(source.height*scale.y));
            RenderTexture previous=RenderTexture.active;
            RenderTexture target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source,target,scale,offset);RenderTexture.active=target;
                Texture2D frame=new Texture2D(width,height,TextureFormat.ARGB32,true);
                frame.name=source.name+"NativeFrame"+index;
                frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.wrapMode=TextureWrapMode.Clamp;
                frame.Apply(true,true);return frame;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        public override Material MatAt(Rot4 rotation, Thing thing=null)
        {
            CompPowerTrader power=thing==null?null:thing.TryGetComp<CompPowerTrader>();
            int state=art.powered&&(power==null||power.PowerOn)?4:0;
            return frames[rotation.AsInt+state];
        }
        public override Material MatSingleFor(Thing thing)
        { return thing==null?MatSingle:MatAt(thing.Rotation,thing); }
        public override Mesh MeshAt(Rot4 rotation) { return MeshPool.GridPlane(art.sizes[rotation.AsInt]); }
        internal float FrameOffset(Rot4 rotation) { return art.offsets[rotation.AsInt]; }
        public override void DrawWorker(Vector3 location,Rot4 rotation,ThingDef definition,Thing thing,float extraRotation)
        { location.z+=FrameOffset(rotation); CrimsonFurnitureDepth.Draw(this,location,rotation,definition,thing,extraRotation); }
        public override void Print(SectionLayer layer,Thing thing,float extraRotation)
        { CrimsonFurnitureDepth.Print(layer,thing,this,art.sizes[thing.Rotation.AsInt],FrameOffset(thing.Rotation)); }
        public override Graphic GetColoredVersion(Shader shader,Color color,Color colorTwo)
        {
            if(shader==ShaderDatabase.Cutout||shader==ShaderDatabase.CutoutComplex){color=Color.white;colorTwo=Color.white;}
            return GraphicDatabase.Get<CrimsonV51FurnitureGraphic>(path,shader,drawSize,color,colorTwo,data,null);
        }
    }

    // The native DBH renderer draws and mirrors this one moving leaf twice.
    // Map its new unchanged PNG into the native left-half/middle-height mask.
    public sealed class CrimsonStallDoorGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);mat=new Material(mat);Rect uv=CrimsonV51ArtRegistration.DoorWindow;
            mat.mainTextureScale=new Vector2(uv.width*2f,uv.height*2f);
            mat.mainTextureOffset=new Vector2(uv.x,uv.y-uv.height*.5f);
            mat.mainTexture.wrapMode=TextureWrapMode.Clamp;
        }
        public override Graphic GetColoredVersion(Shader shader,Color color,Color colorTwo)
        {
            if(shader==ShaderDatabase.Cutout||shader==ShaderDatabase.CutoutComplex){color=Color.white;colorTwo=Color.white;}
            return GraphicDatabase.Get<CrimsonStallDoorGraphic>(path,shader,drawSize,color,colorTwo,data,null);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonV51FurnitureIcons
    {
        static CrimsonV51FurnitureIcons()
        {
            foreach(string name in new[]{"Crimson_DiningTable","Crimson_CookingRefrigerator","Crimson_MealDisplayRefrigerator","Crimson_TacticalTableLarge"})
            {
                ThingDef definition = DefDatabase<ThingDef>.GetNamed(name);
                if (name == "Crimson_MealDisplayRefrigerator" || name == "Crimson_TacticalTableLarge")
                {
                    definition.uiIcon = (Texture2D)definition.graphicData.Graphic.MatSingle.mainTexture;
                    definition.uiIconMaterial = null;
                }
                else CrimsonV48AtlasIcons.Register(definition);
            }
            ThingDef compact=DefDatabase<ThingDef>.GetNamed("Crimson_TacticalTableCompact");
            // Its native frame also serves blueprint shaders that ignore atlas UVs.
            compact.uiIcon=(Texture2D)compact.graphicData.Graphic.MatSouth.mainTexture;
            compact.uiIconMaterial=null;
            ThingDef standard=DefDatabase<ThingDef>.GetNamed("Crimson_TacticalTable");
            standard.uiIcon=(Texture2D)standard.graphicData.Graphic.MatSouth.mainTexture;
            standard.uiIconMaterial=null;
            ThingDef door=DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_ToiletStallDoor");
            if(door==null)return;
            Material icon=new Material(door.graphicData.Graphic.MatAt(Rot4.South,null));
            Rect window=CrimsonV51ArtRegistration.DoorWindow;
            icon.mainTextureScale=new Vector2(window.width,window.height);icon.mainTextureOffset=new Vector2(window.x,window.y);
            CrimsonV48AtlasIcons.Register(door,icon);Object.Destroy(icon);
        }
    }

    public class CompCrimsonFurnitureVisuals : ThingComp
    {
        public override void ReceiveCompSignal(string signal)
        {
            if(parent.Spawned&&(signal=="PowerTurnedOn"||signal=="PowerTurnedOff"||signal=="FlickedOn"||signal=="FlickedOff"))
                parent.DirtyMapMesh(parent.Map);
        }
    }
}
