using UnityEngine;
namespace CrimsonSOS2 {
    internal static class CrimsonV51ArtRegistration {
        internal static readonly Rect DoorWindow = new Rect(0.16507177f,0.22328549f,0.66905901f,0.56778309f);
        internal static CrimsonV51ArtSpec Find(string path) {
            switch(path) {
case "Crimson/Decor/Workshops/ElectricSmelterAtlas": return CrimsonElectricSmelterArt.Create();
case "Crimson/Decor/Workshops/ElectricSmelterRecessedAtlas": return CrimsonElectricSmelterArt.CreateRecessed();
case "Crimson/Decor/Furniture/TacticalTableParallelV137":
case "Crimson/Decor/Furniture/TacticalTableCompactParallelV137":
case "Crimson/Decor/Furniture/TacticalTableLargeParallelV137": return CrimsonTacticalTableArt.Find(path);
case "Crimson/Decor/Research/CrewSofaTwoAtlas":
case "Crimson/Decor/Research/CoffeeTableAtlas":
case "Crimson/Decor/Research/ToolCabinetAtlas":
case "Crimson/Decor/Research/ResearchBenchAtlas":
case "Crimson/Decor/Research/WallUplinkAtlas": return CrimsonResearchArt.Find(path);
case "Crimson/Decor/Seating/CrewSofa3x1Atlas": return CrimsonCrewSofaArt.Create();
case "Crimson/Decor/Recreation/LargeTelevisionWallStates": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.521420519f,0.590755355f,0.252536640f,0.259301015f),new Rect(0.882187148f,0.578354002f,0.038331454f,0.326944758f),new Rect(0.038895152f,0.594137542f,0.247463360f,0.253664036f),new Rect(0.378804961f,0.578354002f,0.038895152f,0.326944758f),new Rect(0.521420519f,0.102593010f,0.252536640f,0.261555806f),new Rect(0.882187148f,0.090191657f,0.038331454f,0.331454340f),new Rect(0.038895152f,0.105975197f,0.247463360f,0.258173619f),new Rect(0.378804961f,0.090191657f,0.038895152f,0.331454340f)}, new[] {new Vector2(3f,1f),new Vector2(1f,3f),new Vector2(3f,1f),new Vector2(1f,3f)}, new[] {0f,0f,0f,0f}, true);
case "Crimson/Decor/Dining/DiningTableLongTopAtlas": return new CrimsonV51ArtSpec(false, new[] {new Rect(0.149739583f,0.584960938f,0.239583333f,0.388671875f),new Rect(0.611328125f,0.584960938f,0.239583333f,0.388671875f),new Rect(0.149739583f,0.083984375f,0.239583333f,0.387695313f),new Rect(0.611328125f,0.083984375f,0.239583333f,0.387695313f)}, new[] {new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f)}, new[] {0.006378f,0.006378f,0.006378f,0.006378f}, true);
case "Crimson/Decor/Dining/DiningTableAtlas": return new CrimsonV51ArtSpec(false, new[] {new Rect(0.15039063f,0.65429688f,0.24088542f,0.29199219f),new Rect(0.60872396f,0.65429688f,0.24088542f,0.29199219f),new Rect(0.15039063f,0.15429688f,0.24088542f,0.29199219f),new Rect(0.60872396f,0.15429688f,0.24088542f,0.29199219f)}, new[] {new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f)}, new[] {0.006378f,0.006378f,0.006378f,0.006378f});
case "Crimson/Decor/Dining/CookingRefrigeratorAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.03213078f,0.52423901f,0.24915445f,0.41600902f),new Rect(0.35400225f,0.52423901f,0.10484780f,0.43630214f),new Rect(0.53213078f,0.52423901f,0.24915445f,0.41600902f),new Rect(0.85400225f,0.52423901f,0.10541150f,0.43630214f),new Rect(0.03213078f,0.02367531f,0.24915445f,0.41600902f),new Rect(0.35400225f,0.02367531f,0.10484780f,0.43630214f),new Rect(0.53213078f,0.02367531f,0.24915445f,0.41600902f),new Rect(0.85400225f,0.02367531f,0.10541150f,0.43630214f)}, new[] {new Vector2(2.060000f,1.719774f),new Vector2(0.880000f,1.830968f),new Vector2(2.060000f,1.719774f),new Vector2(0.880000f,1.821176f)}, new[] {0.448235f,0.053656f,0.448235f,0.048824f});
case "Crimson/Decor/Dining/MealDisplayRefrigeratorAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.02987599f,0.51860203f,0.27846674f,0.36527621f),new Rect(0.35456595f,0.51860203f,0.10879369f,0.42502818f),new Rect(0.51916573f,0.51860203f,0.27452086f,0.36527621f),new Rect(0.85062007f,0.51860203f,0.10766629f,0.42502818f),new Rect(0.02931229f,0.04284104f,0.27903044f,0.36640361f),new Rect(0.35456595f,0.04284104f,0.10879369f,0.42841037f),new Rect(0.51916573f,0.04284104f,0.27508455f,0.36640361f),new Rect(0.85062007f,0.04284104f,0.10766629f,0.42841037f)}, new[] {new Vector2(2.060000f,1.351093f),new Vector2(0.820000f,1.601762f),new Vector2(2.060000f,1.370513f),new Vector2(0.820000f,1.618534f)}, new[] {0.245121f,-0.089741f,0.254682f,-0.081466f});
case "Crimson/Decor/Dining/TacticalTableLargeAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.04735062f,0.51296505f,0.15501691f,0.46448703f),new Rect(0.25310034f,0.58962796f,0.24351747f,0.26719278f),new Rect(0.54735062f,0.51296505f,0.15501691f,0.46448703f),new Rect(0.75310034f,0.58962796f,0.24351747f,0.26719278f),new Rect(0.04735062f,0.01240135f,0.15501691f,0.46448703f),new Rect(0.25310034f,0.08906426f,0.24351747f,0.26719278f),new Rect(0.54735062f,0.01352875f,0.15501691f,0.46335964f),new Rect(0.75310034f,0.09019166f,0.24351747f,0.26606539f)}, new[] {new Vector2(3.040000f,4.554473f),new Vector2(5.040000f,2.765000f),new Vector2(3.040000f,4.554473f),new Vector2(5.040000f,2.765000f)}, new[] {-0.050400f,0.053333f,-0.050400f,0.053333f});
                case "Crimson/Decor/Communications/CommsConsoleAtlas": return CrimsonCommsConsoleArt.Create();
                case "Crimson/Decor/Seating/Premium/LoungeChairAtlas":
                case "Crimson/Decor/Seating/Premium/DiningChairAtlas":
                case "Crimson/Decor/Seating/Premium/SofaAtlas":
                    return CrimsonPremiumSeatingArt.Find(path);
                case "Crimson/Decor/Furniture/TacticalTableCompactAtlas":
                    return new CrimsonV51ArtSpec(true, new[] { new Rect(0.018038331f,0.624577227f,0.214205186f,0.183765502f),new Rect(0.336527621f,0.537767756f,0.077790304f,0.425028185f),new Rect(0.518038331f,0.624577227f,0.214205186f,0.183765502f),new Rect(0.836527621f,0.537767756f,0.077790304f,0.425028185f),new Rect(0.018038331f,0.122886133f,0.214205186f,0.183765502f),new Rect(0.336527621f,0.034949267f,0.077790304f,0.425028185f),new Rect(0.518038331f,0.122886133f,0.214205186f,0.183765502f),new Rect(0.836527621f,0.034949267f,0.077790304f,0.425028185f) },
                        new[] { new Vector2(2.96f,0.98f),new Vector2(0.98f,2.96f),new Vector2(2.96f,0.98f),new Vector2(0.98f,2.96f) },
                        new[] { 0f,0f,0f,0f }, true);
                default: throw new System.ArgumentException("Unknown V51 atlas: "+path);
            }
        }
    }
}
