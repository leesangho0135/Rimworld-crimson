using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonOrganicReconstructor
    {
        internal const string RecipeName = "Crimson_ReconstituteOrganicMatter";

        static CrimsonOrganicReconstructor()
        {
            new Harmony("rimod.crimson.sos2.organic-reconstructor").Patch(
                AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"),
                postfix: new HarmonyMethod(typeof(CrimsonOrganicReconstructor), "Products"));
        }

        private static void Products(RecipeDef recipeDef, List<Thing> ingredients, ref IEnumerable<Thing> __result)
        {
            if (recipeDef == null || recipeDef.defName != RecipeName) return;
            float size = 0f;
            if (ingredients != null)
                foreach (Thing ingredient in ingredients)
                {
                    Corpse corpse = ingredient as Corpse;
                    if (corpse != null && corpse.InnerPawn != null && corpse.InnerPawn.RaceProps.IsFlesh)
                        size += corpse.InnerPawn.BodySize;
                }
            int fuel = FuelYield(size);
            __result = ScaleProducts(__result, fuel);
        }

        internal static int FuelYield(float bodySize)
        {
            if (float.IsNaN(bodySize) || float.IsInfinity(bodySize) || bodySize <= 0f) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(5f * bodySize));
        }

        private static IEnumerable<Thing> ScaleProducts(IEnumerable<Thing> products, int fuel)
        {
            if (products == null) yield break;
            foreach (Thing product in products)
            {
                int count;
                if (product.def.defName == "Chemfuel") count = fuel;
                else if (product.def.defName == "Steel") count = fuel * 2;
                else throw new InvalidOperationException("Unexpected organic reconstitution product: " + product.def.defName);
                if (count <= 0)
                {
                    product.Destroy(DestroyMode.Vanish);
                    continue;
                }
                product.stackCount = count;
                int limit = Math.Max(1, product.def.stackLimit);
                while (product.stackCount > limit) yield return product.SplitOff(limit);
                yield return product;
            }
        }
    }
}
