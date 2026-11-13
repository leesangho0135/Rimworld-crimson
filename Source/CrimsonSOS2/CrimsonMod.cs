using Verse;
using UnityEngine;

namespace CrimsonSOS2
{
    public sealed class CrimsonMod : Mod
    {
        public static CrimsonSettings Settings = new CrimsonSettings();
        private string maximumRadiusBuffer;
        private string defaultRadiusBuffer;

        public CrimsonMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CrimsonSettings>();
            Settings.Normalize();
        }

        public override string SettingsCategory()
        {
            return "Crimson SOS2";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Crimson.Shield.SettingsHelp".Translate());
            listing.Gap(12f);
            NumericRow(listing, "Crimson.Shield.Maximum".Translate(), ref Settings.maximumShieldRadius,
                ref maximumRadiusBuffer, CrimsonSettings.MinimumShieldRadius);
            NumericRow(listing, "Crimson.Shield.Default".Translate(), ref Settings.defaultShieldRadius,
                ref defaultRadiusBuffer, CrimsonSettings.MinimumShieldRadius);
            Settings.Normalize();
            listing.Gap(12f);
            listing.Label("Crimson.Shield.PowerHelp".Translate());
            listing.End();
            CrimsonShieldComp.ApplyDefinitionSettings();
        }

        private static void NumericRow(Listing_Standard listing, string label, ref int value, ref string buffer, int minimum)
        {
            Rect row = listing.GetRect(30f);
            Widgets.Label(new Rect(row.x, row.y, row.width * 0.7f, row.height), label);
            Widgets.TextFieldNumeric(new Rect(row.x + row.width * 0.72f, row.y, row.width * 0.28f, row.height),
                ref value, ref buffer, minimum, int.MaxValue);
        }
    }
}
