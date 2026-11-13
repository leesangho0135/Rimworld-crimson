using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonSeatingReadability
    {
        static CrimsonSeatingReadability()
        {
            MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if(!def.defName.StartsWith("Crimson_",StringComparison.Ordinal)||def.size.x!=1||def.size.z!=1
                    ||def.building==null||!def.building.isSittable
                    ||def.GetCompProperties<CompProperties_CrimsonPoweredComfort>()==null)continue;
                def.comps=new List<CompProperties>(def.comps);
                for(int i=0;i<def.comps.Count;i++)
                {
                    CompProperties_Power original=def.comps[i] as CompProperties_Power;
                    if(original==null)continue;
                    // Electricity enhances comfort; it is not required to sit.
                    // Keep the OFF artwork/inspect status without covering the
                    // usable seat with a full-size required-power overlay.
                    CompProperties_Power adjusted=(CompProperties_Power)clone.Invoke(original,null);
                    adjusted.showPowerNeededIfOff=false;
                    def.comps[i]=adjusted;
                }
            }
        }
    }
}
