using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill
{
    public enum CellHazard
    {
        None,
        Hive,
        Mechanoid,
        Gas
    }

    public class GridCell : IExposable
    {
        public CellHazard hazard;
        public ThingDef? def;

        public int amount;

        public bool unknown;

        public bool HasResource => def != null && amount > 0;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref unknown, "unknown");
            Scribe_Values.Look(ref hazard, "hazard", CellHazard.None);
        }
    }

    public static class DrillGrid
    {
        private struct Candidate
        {
            public ThingDef def;
            public float weight;
            public DrillableResource? entry;
        }

        public static List<GridCell> CreateGrid(CompProperties_ResourceDrill props, int depth)
        {
            var cells = new List<GridCell>(props.gridWidth * props.gridHeight);
            for (int i = 0; i < props.gridWidth * props.gridHeight; i++)
            {
                cells.Add(GenerateCell(props, depth));
            }
            return cells;
        }

        // direction -1: grid moves left, a new column appears on the left and the rightmost column is dropped.
        // direction +1: grid moves right, a new column appears on the right and the leftmost column is dropped.
        public static List<GridCell> Shift(List<GridCell> old, CompProperties_ResourceDrill props, int depth, int direction)
        {
            int w = props.gridWidth;
            int h = props.gridHeight;
            var cells = new List<GridCell>(w * h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX = x - (direction < 0 ? 1 : -1);
                    if (srcX < 0 || srcX >= w)
                    {
                        cells.Add(GenerateCell(props, depth));
                    }
                    else
                    {
                        cells.Add(old[y * w + srcX]);
                    }
                }
            }
            return cells;
        }

        public static GridCell GenerateCell(CompProperties_ResourceDrill props, int depth)
        {
            var cell = GenerateCellContent(props, depth);
            if (Rand.Chance(Mathf.Min(0.5f, (5 + depth) / 100f)))
            {
                cell = new GridCell { hazard = CellHazard.Hive, amount = Mathf.Max(1, depth) };
            }
            else if (Rand.Chance(Mathf.Min(0.3f, (1 + depth) / 100f)))
            {
                cell = new GridCell { hazard = CellHazard.Mechanoid, amount = Mathf.Max(1, depth / 2) };
            }
            else if (Rand.Chance(0.1f))
            {
                cell = new GridCell { hazard = CellHazard.Gas, amount = 1 };
            }
            cell.unknown = Rand.Chance(depth / 50f);
            return cell;
        }

        private static GridCell GenerateCellContent(CompProperties_ResourceDrill props, int depth)
        {
            if (Rand.Chance(props.emptyCellChance))
            {
                return new GridCell();
            }
            var candidates = GetCandidates(props);
            if (candidates.Count == 0)
            {
                return new GridCell();
            }
            var picked = candidates.RandomElementByWeight(c => DepthWeight(c.weight, props, depth));
            float amount = picked.entry != null
                ? picked.entry.amount.RandomInRange
                : picked.def.deepCountPerCell * Rand.Range(0.5f, 1.5f);

            if (Rand.Chance(props.veinChance))
            {
                amount *= props.veinMultiplier;
            }
            else if (Rand.Chance(props.cavernChance))
            {
                amount *= props.cavernMultiplier;
            }

            amount *= 1f + depth * props.depthYieldMultiplierPerLevel;
            amount *= props.resourceYield * Mathf.Log(Mathf.Max(1, depth)) + 1f;
            amount *= ResourceDrillMod.settings.yieldMul;

            int rounded = Mathf.Max(0, Mathf.RoundToInt(amount));
            return rounded > 0 ? new GridCell { def = picked.def, amount = rounded } : new GridCell();
        }

        private static float DepthWeight(float weight, CompProperties_ResourceDrill props, int depth)
        {
            float min = Mathf.Max(1, props.minDepth);
            float max = Mathf.Max(min + 1f, props.maxDepth);
            float t = Mathf.Clamp01((depth - min) / (max - min));
            return Mathf.Pow(Mathf.Max(0.0001f, weight), 1f - 2f * t);
        }

        private static List<Candidate> GetCandidates(CompProperties_ResourceDrill props)
        {
            var result = new List<Candidate>();
            var entries = props.drillableResources;
            bool hasEntries = !entries.NullOrEmpty();
            if (props.allowAnyDeepdrillResource)
            {
                foreach (var def in StaticCollectionCached.drillableResource)
                {
                    if (props.isInvertWhitelist && hasEntries && entries!.Any(x => x.thingDef == def))
                    {
                        continue;
                    }
                    result.Add(new Candidate { def = def, weight = def.deepCommonality });
                }
                if (hasEntries && !props.isInvertWhitelist)
                {
                    AddEntries(result, entries!);
                }
            }
            else if (hasEntries)
            {
                AddEntries(result, entries!);
            }
            return result;
        }

        private static void AddEntries(List<Candidate> result, List<DrillableResource> entries)
        {
            foreach (var entry in entries)
            {
                result.Add(new Candidate { def = entry.thingDef, weight = entry.weight, entry = entry });
            }
        }
    }
}
