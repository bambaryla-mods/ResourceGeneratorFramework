using System.Collections.Generic;
using Verse;

namespace CVN_EnhancedResourceDrill
{
    public static class StaticCollectionCached
    {
        public static HashSet<ThingDef> drillableResource = new HashSet<ThingDef>();

        public static void InitList()
        {
            foreach (var item in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (item.deepCommonality > 0)
                {
                    drillableResource.Add(item);
                }
            }
        }
    }
}