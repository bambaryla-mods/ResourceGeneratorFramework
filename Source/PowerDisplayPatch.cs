using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CVN_EnhancedResourceDrill;

// The build info card reads basePowerConsumption from CompProperties_Power, which is the depth-1 figure.
// Swap the "Power needed" entry for the draw the drill starts at (startDepth).
[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
public static class Patch_ThingDef_SpecialDisplayStats
{
    public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance)
    {
        CompProperties_ResourceDrill? drillProps = __instance.GetCompProperties<CompProperties_ResourceDrill>();
        CompProperties_Power? powerProps = __instance.GetCompProperties<CompProperties_Power>();
        if (drillProps == null || powerProps == null)
        {
            return __result;
        }

        float startPower = CompResourceDrill.PowerDrawAtDepth(powerProps.PowerConsumption, drillProps, CompResourceDrill.StartDepth(drillProps));
        return ReplacePowerEntry(__result, "PowerConsumption".Translate(), startPower.ToString("F0") + " W");
    }

    private static IEnumerable<StatDrawEntry> ReplacePowerEntry(IEnumerable<StatDrawEntry> entries, string label, string value)
    {
        foreach (StatDrawEntry entry in entries)
        {
            if (entry.LabelCap == label)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, label, value, "", 0);
            }
            else
            {
                yield return entry;
            }
        }
    }
}
