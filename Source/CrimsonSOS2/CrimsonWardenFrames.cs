using UnityEngine;
namespace CrimsonSOS2
{
    internal static class CrimsonWardenFrames
    {
        internal static readonly string[] Bodies = { "Male", "Female", "Thin", "Fat", "Hulk" };
        private static readonly Rect[] StaffUniformSources = {
            R(51,56,218,251), R(368,61,203,246), R(680,61,166,246), R(932,55,248,253), R(1232,46,281,263),
            R(92,387,139,253), R(400,387,140,253), R(706,388,116,251), R(975,383,173,258), R(1285,375,186,265),
            R(49,727,224,250), R(366,730,209,247), R(678,726,173,251), R(932,724,248,254), R(1232,710,285,269) };
        private static readonly Rect[] StaffHelmetSources = {
            R(220,35,327,421), R(945,37,375,417), R(218,553,333,419), R(982,553,340,421) };
        internal static Rect R(float x,float y,float w,float h) { return new Rect(x,y,w,h); }
        internal static Rect Source(string item, int body, int row)
        {
            Rect ranked;
            if (CrimsonRankEquipmentArt.TrySource(item, row, out ranked)) return ranked;
            if (item == "StaffUniform") return StaffUniformSources[row * 5 + body];
            if (item == "StaffHelmet") return StaffHelmetSources[row];
            switch(item)
            {
                case "Helmet":
                    switch(row) {
                        case 0: return R(235,12,301,484);
                        case 1: return R(961,15,349,479);
                        case 2: return R(226,527,321,483);
                        case 3: return R(997,527,313,485);
                    } break;
                case "Uniform":
                    switch(row*5+body) {
                        case 0: return R(51,57,217,249);
                        case 1: return R(371,61,197,246);
                        case 2: return R(681,61,166,245);
                        case 3: return R(934,56,245,252);
                        case 4: return R(1232,47,280,261);
                        case 5: return R(93,388,137,250);
                        case 6: return R(401,388,138,250);
                        case 7: return R(706,389,118,249);
                        case 8: return R(976,384,174,256);
                        case 9: return R(1285,374,186,266);
                        case 10: return R(50,727,222,250);
                        case 11: return R(369,730,204,247);
                        case 12: return R(680,726,170,250);
                        case 13: return R(933,725,247,253);
                        case 14: return R(1233,710,283,270);
                    } break;
                case "Cloak":
                    switch(row*5+body) {
                        case 0: return R(77,34,210,277);
                        case 1: return R(388,40,194,268);
                        case 2: return R(687,40,155,268);
                        case 3: return R(933,34,235,278);
                        case 4: return R(1213,27,296,287);
                        case 5: return R(105,354,127,297);
                        case 6: return R(410,357,120,289);
                        case 7: return R(711,355,100,291);
                        case 8: return R(955,354,157,297);
                        case 9: return R(1252,345,186,311);
                        case 10: return R(79,696,210,290);
                        case 11: return R(389,703,193,280);
                        case 12: return R(688,703,153,280);
                        case 13: return R(930,697,243,288);
                        case 14: return R(1216,687,295,303);
                    } break;
                case "Sword":
                    switch(row) {
                        case 0: return R(95,106,1078,1086);
                    } break;
                case "Pistol":
                    switch(row) {
                        case 0: return R(225,352,806,581);
                    } break;
            }
            throw new System.ArgumentOutOfRangeException("item");
        }
        internal static Rect Target(string item, int body, int row)
        {
            if (CrimsonRankEquipmentArt.IsHead(item)) return row==1 ? R(43,48,142,151) : R(56,44,144,155);
            if (item == "OfficerCoat") return Target("Cloak", body, row);
            if (item == "AssistDrones")
            {
                Rect uniform = Target("Uniform", body, row);
                Rect source = Source(item, body, row);
                float width = Mathf.Min(248f, uniform.width + (row == 1 ? 38f : 66f));
                width = Mathf.Min(width, 2f * Mathf.Min(uniform.center.x, 256f - uniform.center.x));
                float height = width * source.height / source.width;
                return R(uniform.center.x - width / 2f, uniform.y - 12f, width, height);
            }
            switch(row*5+body) {
                case 0: return item=="Cloak" ? R(40,79,177,148) : R(49,82,159,141);
                case 5: return item=="Cloak" ? R(69,68,115,159) : R(78,71,97,152);
                case 10: return item=="Cloak" ? R(40,78,177,149) : R(49,81,159,142);
                case 1: return item=="Cloak" ? R(60,71,136,156) : R(69,74,118,149);
                case 6: return item=="Cloak" ? R(69,68,103,158) : R(78,71,85,151);
                case 11: return item=="Cloak" ? R(62,71,132,156) : R(71,74,114,149);
                case 2: return item=="Cloak" ? R(68,93,120,129) : R(77,96,102,122);
                case 7: return item=="Cloak" ? R(80,77,93,149) : R(89,80,75,142);
                case 12: return item=="Cloak" ? R(66,91,124,132) : R(75,94,106,125);
                case 3: return item=="Cloak" ? R(30,69,197,174) : R(39,72,179,167);
                case 8: return item=="Cloak" ? R(48,65,153,170) : R(57,68,135,163);
                case 13: return item=="Cloak" ? R(30,70,210,163) : R(39,73,192,156);
                case 4: return item=="Cloak" ? R(30,61,197,194) : R(39,64,179,189);
                case 9: return item=="Cloak" ? R(49,58,147,197) : R(58,61,129,191);
                case 14: return item=="Cloak" ? R(30,60,210,195) : R(39,63,192,190);
            }
            throw new System.ArgumentOutOfRangeException("body");
        }
    }
}
