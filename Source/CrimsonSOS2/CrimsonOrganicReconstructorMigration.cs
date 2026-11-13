using System.Xml;
using HarmonyLib;
using Verse;

namespace CrimsonSOS2
{
    // A save's explicit Class attribute takes precedence over ThingDef.thingClass.
    // Redirect only the legacy organic machine in the loader's in-memory XML,
    // before Scribe constructs it. Its IDs, bills, HP, rotation and comps stay
    // in the same node; the user's original save file is never rewritten here.
    internal static class CrimsonOrganicReconstructorMigration
    {
        internal static int UpgradeLegacyNodes(XmlNode root)
        {
            if (root == null || root.Name != "savegame" || root.SelectSingleNode("game") == null) return 0;
            XmlNodeList nodes = root.SelectNodes(".//thing[def='Crimson_OrganicReconstructor' and "
                + "(@Class='Building_WorkTable_HeatPush' or @Class='RimWorld.Building_WorkTable_HeatPush')]");
            int changed = 0;
            foreach (XmlNode node in nodes)
            {
                node.Attributes["Class"].Value = typeof(CrimsonOrganicReconstructorQueue).FullName;
                changed++;
            }
            return changed;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonOrganicReconstructorMigrationRegistration
    {
        static CrimsonOrganicReconstructorMigrationRegistration()
        {
            new Harmony("rimod.crimson.sos2.organic-legacy-load").Patch(
                AccessTools.Method(typeof(ScribeLoader), "InitLoading", new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(CrimsonOrganicReconstructorMigrationRegistration), "PrepareLegacyMachines"));
        }

        private static void PrepareLegacyMachines(ScribeLoader __instance)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars || __instance == null) return;
            int changed = CrimsonOrganicReconstructorMigration.UpgradeLegacyNodes(__instance.curXmlParent);
            if (changed > 0) Log.Message("Crimson: upgraded " + changed
                + " legacy organic reconstructor(s) to automatic processing during load.");
        }
    }
}
