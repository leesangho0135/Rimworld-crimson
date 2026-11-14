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
case "Crimson/Decor/Research/CoffeeTableAtlas": return CrimsonResearchArt.Find(path);
case "Crimson/Decor/Seating/CrewSofa3x1Atlas": return CrimsonCrewSofaArt.Create();
case "Crimson/Decor/Recreation/LargeTelevisionWallStates": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.521420519f,0.590755355f,0.252536640f,0.259301015f),new Rect(0.882187148f,0.578354002f,0.038331454f,0.326944758f),new Rect(0.038895152f,0.594137542f,0.247463360f,0.253664036f),new Rect(0.378804961f,0.578354002f,0.038895152f,0.326944758f),new Rect(0.521420519f,0.102593010f,0.252536640f,0.261555806f),new Rect(0.882187148f,0.090191657f,0.038331454f,0.331454340f),new Rect(0.038895152f,0.105975197f,0.247463360f,0.258173619f),new Rect(0.378804961f,0.090191657f,0.038895152f,0.331454340f)}, new[] {new Vector2(3f,1f),new Vector2(1f,3f),new Vector2(3f,1f),new Vector2(1f,3f)}, new[] {0f,0f,0f,0f}, true);
case "Crimson/Decor/Dining/DiningTableLongTopAtlas": return new CrimsonV51ArtSpec(false, new[] {new Rect(0.149739583f,0.584960938f,0.239583333f,0.388671875f),new Rect(0.611328125f,0.584960938f,0.239583333f,0.388671875f),new Rect(0.149739583f,0.083984375f,0.239583333f,0.387695313f),new Rect(0.611328125f,0.083984375f,0.239583333f,0.387695313f)}, new[] {new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f),new Vector2(2.08f,2.4f)}, new[] {0.006378f,0.006378f,0.006378f,0.006378f}, true);
case "Crimson/Decor/Dining/DiningTableAtlas": return new CrimsonV51ArtSpec(false, new[] {new Rect(0.15039063f,0.65429688f,0.24088542f,0.29199219f),new Rect(0.60872396f,0.65429688f,0.24088542f,0.29199219f),new Rect(0.15039063f,0.15429688f,0.24088542f,0.29199219f),new Rect(0.60872396f,0.15429688f,0.24088542f,0.29199219f)}, new[] {new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f),new Vector2(2.080000f,1.680865f)}, new[] {0.006378f,0.006378f,0.006378f,0.006378f});
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
