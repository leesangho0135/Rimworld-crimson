using UnityEngine;

namespace CrimsonSOS2
{
    // Three authored views share the established five-body fitting rectangles.
    // West uses the game's mirrored east mesh, exactly like the existing set.
    internal static class CrimsonRankEquipmentArt
    {
        internal static bool IsHead(string item)
        {
            return item == "Helmet" || item == "StaffHelmet" || item == "SoldierHelmet";
        }

        internal static string TexturePath(string item)
        {
            string name = item == "Helmet" ? "LeaderHood" : item == "StaffHelmet" ? "OfficerCap" : item;
            return IsHead(item) || item == "OfficerCoat" || item == "SoldierUniform" || item == "AssistDrones"
                ? "Crimson/Equipment/RanksV173/" + name : "Crimson/Equipment/" + item;
        }

        internal static bool TrySource(string item, int row, out Rect source)
        {
            Rect[] frames;
            switch (item)
            {
                case "Helmet": frames = Hood; break;
                case "StaffHelmet": frames = Cap; break;
                case "SoldierHelmet": frames = Helmet; break;
                case "OfficerCoat": frames = Coat; break;
                case "SoldierUniform": frames = Uniform; break;
                case "AssistDrones": frames = Drones; break;
                default: source = new Rect(); return false;
            }
            source = frames[row == 3 ? 0 : row];
            return true;
        }

        private static readonly Rect[] Hood = {
            new Rect(109,19,294,477), new Rect(603,18,329,478), new Rect(1126,16,307,480) };
        private static readonly Rect[] Cap = {
            new Rect(49,17,413,479), new Rect(556,18,424,478), new Rect(1069,16,422,480) };
        private static readonly Rect[] Helmet = {
            new Rect(47,16,418,480), new Rect(561,16,413,480), new Rect(1063,17,434,479) };
        private static readonly Rect[] Coat = {
            new Rect(110,18,292,478), new Rect(677,16,182,480), new Rect(1133,17,293,479) };
        private static readonly Rect[] Uniform = {
            new Rect(59,16,394,480), new Rect(640,19,255,477), new Rect(1082,16,395,480) };
        private static readonly Rect[] Drones = {
            new Rect(21,359,469,137), new Rect(550,318,435,178), new Rect(1040,360,480,136) };
    }
}
