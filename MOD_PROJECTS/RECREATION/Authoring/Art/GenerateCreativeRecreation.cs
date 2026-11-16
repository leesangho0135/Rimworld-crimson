// Vector source for the two alien recreation devices. Render with the existing
// .NET Framework compiler and System.Drawing; no external assets or packages.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

internal static class GenerateCreativeRecreation
{
    private const int Width = 400, Height = 512;
    private struct V
    {
        internal float x, y, z;
        internal V(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
    }
    private sealed class Shape
    {
        internal PointF[] points;
        internal Color fill;
        internal float depth;
    }
    private sealed class Scene
    {
        internal readonly List<Shape> shapes = new List<Shape>();
        private readonly int direction;
        internal readonly bool powered;
        internal Scene(int direction, bool powered) { this.direction=direction; this.powered=powered; }
        private V Rotate(V v)
        {
            for (int i=0;i<direction;i++) { float x=v.x; v.x=v.z; v.z=-x; }
            return v;
        }
        private PointF Project(V v) { return new PointF(200+v.x*160,365-v.z*80-v.y*152); }
        internal void Face(Color color, params V[] vertices)
        {
            PointF[] points = new PointF[vertices.Length];
            float depth = 0;
            for (int i=0;i<vertices.Length;i++)
            {
                V v=Rotate(vertices[i]); points[i]=Project(v); depth += -v.z+v.y*.05f;
            }
            shapes.Add(new Shape { points=points, fill=color, depth=depth/vertices.Length });
        }
        internal Color Signal { get { return powered ? C("ed3850") : C("542631"); } }
        internal void Plate(float radius, float y, float thickness)
        {
            const int count=8;
            V[] top=new V[count];
            for(int i=0;i<count;i++)
            {
                double a=(i+.5)*Math.PI*2/count;
                top[i]=new V((float)Math.Cos(a)*radius,y,(float)Math.Sin(a)*radius);
            }
            for(int i=0;i<count;i++)
            {
                V a=top[i], b=top[(i+1)%count];
                Face(i%2==0?C("282d32"):C("181c22"),new V(a.x,y-thickness,a.z),new V(b.x,y-thickness,b.z),b,a);
            }
            Face(C("353a3f"),top);
        }
        internal void Ring(float cx,float cy,float cz,float radius,float lean,int start,int end)
        {
            for(int deg=start;deg<end;deg+=8)
            {
                V a=RingPoint(cx,cy,cz,radius,lean,deg);
                V b=RingPoint(cx,cy,cz,radius,lean,Math.Min(deg+8,end));
                V c=RingPoint(cx,cy,cz,radius-.09f,lean,Math.Min(deg+8,end));
                V d=RingPoint(cx,cy,cz,radius-.09f,lean,deg);
                Face(C("3d4248"),a,b,c,d);
                Face(C("14181e"),new V(a.x,a.y,a.z+.08f),new V(b.x,b.y,b.z+.08f),b,a);
                if(deg%24!=0)
                {
                    V e=RingPoint(cx,cy,cz,radius-.065f,lean,deg);
                    V f=RingPoint(cx,cy,cz,radius-.065f,lean,Math.Min(deg+6,end));
                    V g=RingPoint(cx,cy,cz,radius-.085f,lean,Math.Min(deg+6,end));
                    V h=RingPoint(cx,cy,cz,radius-.085f,lean,deg);
                    Face(Signal,e,f,g,h);
                }
            }
        }
        private static V RingPoint(float cx,float cy,float cz,float r,float lean,int deg)
        {
            double a=deg*Math.PI/180;
            float x=(float)Math.Cos(a)*r, y=(float)Math.Sin(a)*r;
            return new V(cx+x,cy+y,cz+x*lean);
        }
        internal void Crystal(float x,float y,float z,float width,float height,float lean)
        {
            V top=new V(x+lean,y+height,z+.04f), bottom=new V(x-lean*.2f,y-height*.44f,z);
            V a=new V(x-width,y,z-.03f),b=new V(x+width*.3f,y+.09f,z-width*.7f);
            V c=new V(x+width,y+.02f,z+.03f),d=new V(x-width*.2f,y-.06f,z+width*.7f);
            Face(C("652731"),top,a,b); Face(C("a63a49"),top,b,c);
            Face(C("3f222b"),top,c,d); Face(C("78313d"),top,d,a);
            Face(C("221b24"),bottom,b,a); Face(C("4d2530"),bottom,c,b);
            Face(C("271d27"),bottom,d,c); Face(C("40232c"),bottom,a,d);
            Face(Signal,new V(x+.02f,y+.08f,z-width*.71f),new V(x+.07f,y+.08f,z-width*.71f),
                new V(top.x+.015f,top.y-.12f,top.z-.015f));
        }
        internal void Trace(float x,float z,float length,bool horizontal)
        {
            float y=.113f;
            if(horizontal) Face(Signal,new V(x,y,z),new V(x+length,y,z),new V(x+length,y,z+.018f),new V(x,y,z+.018f));
            else Face(Signal,new V(x,y,z),new V(x+.018f,y,z),new V(x+.018f,y,z+length),new V(x,y,z+length));
        }
        internal void Spine(float y,float z,float width)
        {
            V left=new V(-width,y,z),right=new V(width,y,z),top=new V(0,y+.12f,z+.06f),front=new V(0,y-.07f,z-.13f);
            Face(C("2a3037"),top,left,front); Face(C("50565c"),top,front,right);
            Face(C("131820"),left,new V(0,y-.16f,z),right,front);
            Face(Signal,new V(-width*.5f,y-.025f,z-.065f),front,new V(width*.5f,y-.025f,z-.065f),new V(0,y-.032f,z-.14f));
        }
    }
    private static Color C(string hex) { return ColorTranslator.FromHtml("#"+hex); }
    private static Scene Device(bool memory,int direction,bool on)
    {
        Scene s=new Scene(direction,on);
        s.Plate(.97f,0,.09f); s.Plate(.83f,.10f,.10f);
        s.Trace(-.55f,-.46f,.35f,true); s.Trace(.22f,-.46f,.30f,true);
        s.Trace(-.56f,-.44f,.24f,false); s.Trace(.52f,-.44f,.24f,false);
        if(!memory)
        {
            s.Plate(.30f,.23f,.12f);
            s.Ring(-.03f,1.06f,.14f,.79f,.42f,18,166);
            s.Ring(-.03f,1.06f,.14f,.79f,.42f,195,336);
            s.Crystal(-.08f,.92f,.0f,.24f,.75f,.18f);
            s.Crystal(-.59f,.92f,-.18f,.10f,.22f,-.06f);
            s.Crystal(.52f,1.47f,.02f,.085f,.21f,.04f);
            s.Crystal(.39f,.45f,-.22f,.075f,.17f,-.02f);
        }
        else
        {
            // A fragmented neural spine and open crown: no work surface/tools.
            s.Plate(.26f,.22f,.12f);
            for(int i=0;i<6;i++) s.Spine(.43f+i*.20f,.21f+(float)Math.Sin(i*.6f)*.11f,.22f-i*.018f);
            s.Ring(0,1.20f,.17f,.66f,-.20f,0,154);
            s.Ring(0,1.20f,.17f,.66f,-.20f,200,277);
            s.Crystal(0,1.68f,.17f,.105f,.23f,.04f);
            // Segmented tendrils converge on the visitor's side of the crown.
            for(int side=-1;side<=1;side+=2) for(int i=0;i<5;i++)
            {
                float t=i/5f, next=(i+.68f)/5f;
                V a=new V(side*(.59f-t*.30f),1.18f-t*.76f,.08f-t*.66f);
                V b=new V(side*(.59f-next*.30f),1.18f-next*.76f,.08f-next*.66f);
                s.Face(s.Signal,a,b,new V(b.x+side*.022f,b.y,b.z),new V(a.x+side*.022f,a.y,a.z));
            }
        }
        return s;
    }
    private static string Number(float f) { return f.ToString("0.###",CultureInfo.InvariantCulture); }
    private static void Render(string root,bool memory)
    {
        string key=memory?"CraftMemoryInfuser":"AlienArtResonator";
        StringBuilder svg=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1600\" height=\"1024\" viewBox=\"0 0 1600 1024\">\n");
        using(Bitmap atlas=new Bitmap(Width*4,Height*2,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(atlas))
        {
            g.Clear(Color.Transparent); g.SmoothingMode=SmoothingMode.AntiAlias;
            for(int state=0;state<2;state++) for(int dir=0;dir<4;dir++)
            {
                Scene scene=Device(memory,dir,state==1);
                scene.shapes.Sort(delegate(Shape a,Shape b) { return a.depth.CompareTo(b.depth); });
                g.TranslateTransform(dir*Width,state*Height);
                svg.Append("<g transform=\"translate(").Append(dir*Width).Append(',').Append(state*Height).Append(")\">\n");
                foreach(Shape shape in scene.shapes)
                {
                    using(SolidBrush brush=new SolidBrush(shape.fill)) g.FillPolygon(brush,shape.points);
                    svg.Append("<polygon fill=\"").Append(ColorTranslator.ToHtml(shape.fill)).Append("\" points=\"");
                    foreach(PointF p in shape.points) svg.Append(Number(p.X)).Append(',').Append(Number(p.Y)).Append(' ');
                    svg.Append("\"/>\n");
                }
                svg.Append("</g>\n"); g.ResetTransform();
            }
            atlas.Save(Path.Combine(root,"MOD_PROJECTS/RECREATION/Content/1.6/Textures/Crimson/Decor/Recreation/"+key+".png"),ImageFormat.Png);
        }
        svg.Append("</svg>\n");
        File.WriteAllText(Path.Combine(root,"MOD_PROJECTS/RECREATION/Authoring/Art/"+key+".svg"),svg.ToString(),new UTF8Encoding(false));
    }
    public static void Main(string[] args)
    {
        if(args.Length!=1) throw new ArgumentException("Pass the project root.");
        Render(args[0],false); Render(args[0],true);
    }
}
