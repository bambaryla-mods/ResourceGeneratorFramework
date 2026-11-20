using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill
{
    public class CompProperties_ResourceDrill : CompProperties
    {
        public bool allowAnyDeepdrillResource = true;

        public List<DrillableResource>? drillableResources;

        public bool isInvertWhitelist = false;

        // Legacy fields from the old resource-select drill. Kept so existing XML still loads; no longer used.
        public int baseWorkAmount = 60;

        public bool isFixedWorkAmount = false;

        public string? selectResourceUiIcon;

        public JobDef operateJobDef = null!;

        // Automatic drills do all actions by themselves: every tickInterval ticks they add workPerInterval work.
        public bool isAutomatic = false;

        public int tickInterval = 60;

        public int workPerInterval = 1;

        public float resourceYield = 1f;

        public Texture2D? selectResourceUiIconTex;

        // Size of the underground grid.
        public int gridWidth = 2;

        public int gridHeight = 2;

        // 0 = drill all only, 1 = also drill leftmost column / top row, 2 = click a cell to drill it
        public int preciseDrilling = 0;

        // Chance that a freshly generated cell contains nothing.
        public float emptyCellChance = 0.3f;

        // Chance that a cell becomes a "vein" (amount multiplied by veinMultiplier).
        public float veinChance = 0.1f;

        public float veinMultiplier = 2f;

        // Chance that a cell becomes a "cavern" (amount multiplied by cavernMultiplier).
        public float cavernChance = 0.1f;

        public float cavernMultiplier = 0.5f;

        // Work needed for each action.
        public int workDrill = 600;

        public int workMoveSideways = 300;

        public int workMoveUp = 300;

        public int workMoveDown = 600;

        // Depth settings: yield multiplier is 1 + depth * depthYieldMultiplierPerLevel,
        // power draw is the base power consumption at startDepth plus powerPerDepth for each level above level 1 (basePowerConsumption is the level 1 figure).
        public int minDepth = 1;
                public int startDepth = 1;
                public int maxDepth = 10;

        public float powerPerDepth = 0f;

        public float depthYieldMultiplierPerLevel = 0f;

        // Cost of drilling deeper. Null def means going deeper is free.
        // Cost is expandDownCostCount + expandDownCostCountPerDepth * currentDepth.
        public ThingDef? expandDownCostDef;

        public int expandDownCostCount = 0;

        public int expandDownCostCountPerDepth = 0;

        // Produced when drilling an empty grid.
        public ThingDef? stoneChunkDef;

        public int stoneChunkCount = 2;

        public CompProperties_ResourceDrill()
        {
            compClass = typeof(CompResourceDrill);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (selectResourceUiIcon != null)
                {
                    selectResourceUiIconTex = ContentFinder<Texture2D>.Get(selectResourceUiIcon);
                }
            });
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (var error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (gridWidth < 1 || gridHeight < 1)
            {
                yield return "CompProperties_ResourceDrill: gridWidth and gridHeight must be at least 1.";
            }
        }
    }

    public class DrillableResource
    {
        public ThingDef thingDef = null!;

        public IntRange amount;

        public float weight = 1f;

        // Legacy fields, kept so existing XML still loads; no longer used.
        public bool fixedWorkAmount;

        public int workAmount = 60;
    }
}
