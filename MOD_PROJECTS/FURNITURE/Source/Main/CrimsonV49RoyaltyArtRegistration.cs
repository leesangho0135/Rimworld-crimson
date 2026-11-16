using UnityEngine;
namespace CrimsonSOS2 {
    internal static class CrimsonV49RoyaltyArtRegistration {
        internal static bool TryFind(string path, out CrimsonV48ArtSpec result) {
            switch (path) {
                case "Crimson/Decor/Royalty/PianoAtlas":
                    result = new CrimsonV48ArtSpec(new[] { new Rect(0.039872f,0.507974f,0.376396f,0.464115f),new Rect(0.433812f,0.534290f,0.534290f,0.393142f),new Rect(0.034290f,0.027113f,0.384370f,0.464115f),new Rect(0.459330f,0.045455f,0.517544f,0.393142f) },
                        new[] { new Vector2(3.150000f,3.500000f),new Vector2(3.150000f,3.500000f),new Vector2(3.150000f,3.500000f),new Vector2(3.150000f,3.500000f) }, new[] { 0.300000f,0.300000f,0.300000f,0.300000f }); return true;
                case "Crimson/Decor/Royalty/PianoStoolAtlas":
                    result = new CrimsonV48ArtSpec(new[] { new Rect(0.122807f,0.661882f,0.255183f,0.158692f),new Rect(0.677831f,0.651515f,0.143541f,0.170654f),new Rect(0.115630f,0.153110f,0.268740f,0.151515f),new Rect(0.677831f,0.149920f,0.144338f,0.165869f) },
                        new[] { new Vector2(0.779300f,0.484627f),new Vector2(0.438356f,0.521157f),new Vector2(0.820700f,0.462709f),new Vector2(0.440791f,0.506545f) }, new[] { 0.022314f,0.040578f,0.011355f,0.033272f }); return true;
                default: result = null; return false;
            }
        }
    }
}
