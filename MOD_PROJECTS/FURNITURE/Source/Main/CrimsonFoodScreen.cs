using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // One immutable, illustrative food inventory display. No random image or live stock claim.
    internal static class CrimsonFoodScreen
    {
        private static Material material;
        internal static Material Material
        {
            get
            {
                if (material == null)
                    material = MaterialPool.MatFrom(new MaterialRequest(Texture, ShaderDatabase.TransparentPostLight));
                return material;
            }
        }
        private static Texture2D texture;
        internal static Texture2D Texture
        {
            get
            {
                if (texture != null) return texture;
                texture = new Texture2D(160, 300, TextureFormat.RGBA32, false);
                texture.name = "CrimsonFoodStockFixed";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                pixels = new Color32[160 * 300];
                for (int i=0;i<pixels.Length;i++) pixels[i]=new Color32(12,2,4,255);
                Box(7,7,146,286,new Color32(80,8,18,255));
                Box(9,9,142,282,new Color32(12,2,4,255));
                Text("FOOD",17,270,3); Text("STOCK",17,244,2);
                Box(16,232,128,2,Red);
                string[] names={"MEAL","VEG","MEAT","MILK"};
                string[] values={"84","62","48","36"}; int[] bars={90,68,52,39};
                for(int row=0;row<4;row++)
                {
                    int y=211-row*27;Text(names[row],17,y,1);Text(values[row],123,y,1);
                    Box(17,y-8,126,1,Dim);Box(17,y-5,bars[row],3,Red);
                }
                Text("FRESH",17,91,1);Text("-5C",115,91,1);
                for(int x=17;x<=143;x+=21)Box(x,28,1,50,Dim);
                for(int y=28;y<=78;y+=10)Box(17,y,126,1,Dim);
                int[] graph={42,46,44,56,51,64,62};
                for(int i=0;i<graph.Length-1;i++)Line(17+i*21,graph[i],38+i*21,graph[i+1]);
                texture.SetPixels32(pixels);texture.Apply(false,true);pixels=null;
                return texture;
            }
        }
        private static Color32[] pixels;
        private static readonly Color32 Red=new Color32(242,32,48,255), Dim=new Color32(72,10,20,255);
        private static void Box(int x,int y,int w,int h,Color32 color)
        { for(int v=y;v<y+h;v++)for(int u=x;u<x+w;u++)if(u>=0&&u<160&&v>=0&&v<300)pixels[v*160+u]=color; }
        private static void Line(int x0,int y0,int x1,int y1)
        { for(int x=x0;x<=x1;x++){int y=Mathf.RoundToInt(Mathf.Lerp(y0,y1,(float)(x-x0)/(x1-x0)));Box(x,y,2,2,Red);} }
        private static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>
        {
            {'F',"111/100/110/100/100"},{'O',"111/101/101/101/111"},{'D',"110/101/101/101/110"},
            {'S',"111/100/111/001/111"},{'T',"111/010/010/010/010"},{'C',"111/100/100/100/111"},
            {'K',"101/101/110/101/101"},{'M',"101/111/111/101/101"},{'E',"111/100/110/100/111"},
            {'A',"010/101/111/101/101"},{'L',"100/100/100/100/111"},{'V',"101/101/101/101/010"},
            {'G',"111/100/101/101/111"},{'I',"111/010/010/010/111"},{'R',"110/101/110/101/101"},
            {'H',"101/101/111/101/101"},{'-',"000/000/111/000/000"},
            {'8',"111/101/111/101/111"},{'4',"101/101/111/001/001"},{'6',"111/100/111/101/111"},
            {'2',"111/001/111/100/111"},{'3',"111/001/111/001/111"},{'5',"111/100/111/001/111"}
        };
        private static void Text(string text,int x,int y,int scale)
        {
            foreach(char c in text)
            {
                string glyph;if(Glyphs.TryGetValue(c,out glyph))
                {string[] rows=glyph.Split('/');for(int r=0;r<5;r++)for(int col=0;col<3;col++)if(rows[r][col]=='1')Box(x+col*scale,y-r*scale,scale,scale,Red);}
                x+=4*scale;
            }
        }
    }
}
