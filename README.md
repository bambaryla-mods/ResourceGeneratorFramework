# Resource Generator Framework (RGF)

A RimWorld 1.7 framework for resource drill buildings. It adds nothing to the game by itself. Other mods use it to define drills that dig resources out of an underground grid.

Full docs with a field reference and copy-paste XML: https://bambaryla-mods.github.io/ResourceGeneratorFramework/

## How a drill plays

Each drill has a small underground grid (2x2 by default). Every cell holds a resource stack, nothing, or a hazard. From the drill's inspector tab the player queues up to 10 actions:

- Drill: empty the whole grid, or just a column, row or single cell if the drill allows precise drilling.
- Move left or right: shift the grid one column. A fresh column is generated on one side and the other side is lost. Each move damages the drill a little, more at depth.
- Drill deeper or drill up: change depth and roll a new grid. Going down can cost materials.

Deeper levels give bigger stacks, favor rarer resources, take longer to work and can draw more power. They also have more hazards: insect hives, dormant mechanoids and methane pockets that explode. Some deep cells are hidden until drilled.

Output drops next to the drill. If Vanilla Expanded Framework's pipe system is loaded, matching resources go straight into an adjacent or attached pipe net first.

Manual drills are worked by pawns assigned to the WorkGiverDef's work type. Work speed uses the Deep Drilling Speed stat, and several pawns can work one drill if it has more than one interaction spot. Automatic drills work on their own.

## Using it in your mod

1. Add Resource Generator Framework as a dependency (`bambaryla.resourcegeneratorframework`).
2. Give your building `ThingDef` the `CVN_EnhancedResourceDrill.CompProperties_ResourceDrill` comp and the `CVN_EnhancedResourceDrill.ITab_ResourceDrill` inspector tab.
3. For manual drills, add a `WorkGiverDef` with `giverClass` `CVN_EnhancedResourceDrill.WorkGiver_DrillAtResourceDrill` and a `CVN_EnhancedResourceDrill.DefModExtension_ResourceDrill` that names your drill and the `CVN_OperateResourceDrill` job.

`1.7/Defs/example.xml` has a full commented-out drill. A minimal WorkGiverDef:

```xml
<WorkGiverDef>
  <defName>MyMod_DrillAtMyDrill</defName>
  <label>drill at resource drill</label>
  <giverClass>CVN_EnhancedResourceDrill.WorkGiver_DrillAtResourceDrill</giverClass>
  <workType>Mining</workType>
  <priorityInType>50</priorityInType>
  <verb>drill</verb>
  <gerund>drilling</gerund>
  <requiredCapacities>
    <li>Manipulation</li>
  </requiredCapacities>
  <modExtensions>
    <li Class="CVN_EnhancedResourceDrill.DefModExtension_ResourceDrill">
      <DrillThingDef>MyMod_Drill</DrillThingDef>
      <DrillJobDef>CVN_OperateResourceDrill</DrillJobDef>
    </li>
  </modExtensions>
</WorkGiverDef>
```

To let several pawns work one drill, give the building more interaction spots:

```xml
<multipleInteractionCellOffsets>
  <li>(0,0,-2)</li>
  <li>(1,0,-2)</li>
</multipleInteractionCellOffsets>
```

## Comp fields

Fields on `CompProperties_ResourceDrill`, with defaults:

| Field | Default | What it does |
|---|---|---|
| `operateJobDef` | none | Set to `CVN_OperateResourceDrill`. Required for manual drills. |
| `isAutomatic` | `false` | Drill works without pawns. |
| `tickInterval` / `workPerInterval` | `60` / `1` | Automatic drills add `workPerInterval` work every `tickInterval` ticks. |
| `gridWidth` / `gridHeight` | `2` / `2` | Grid size. Both must be at least 1. |
| `preciseDrilling` | `0` | 0: drill the whole grid only. 1: also drill the leftmost column or top row. 2: click any cell to drill it. Partial drills cost a matching share of `workDrill`. |
| `workDrill` | `600` | Work to drill the whole grid. |
| `workMoveSideways` / `workMoveUp` / `workMoveDown` | `300` / `300` / `600` | Work per move. All work costs are multiplied by current depth. |
| `minDepth` / `startDepth` / `maxDepth` | `1` / `1` / `10` | Depth range and where a new drill starts. |
| `powerPerDepth` | `0` | Extra watts for each level past level 1, on top of `basePowerConsumption`. |
| `depthYieldMultiplierPerLevel` | `0` | Stack size multiplier `1 + depth * value`. |
| `resourceYield` | `1` | Scales a second depth bonus, `1 + resourceYield * ln(depth)`. Has no effect at depth 1. |
| `expandDownCostDef` | none | Material paid to go deeper. Leave empty to make it free. |
| `expandDownCostCount` / `expandDownCostCountPerDepth` | `0` / `0` | Cost is `count + perDepth * currentDepth`, taken from unforbidden stacks anywhere on the map. |
| `emptyCellChance` | `0.3` | Chance a cell is empty. |
| `veinChance` / `veinMultiplier` | `0.1` / `2` | Chance a cell is a vein and its stack multiplier. |
| `cavernChance` / `cavernMultiplier` | `0.1` / `0.5` | Same for caverns, which shrink the stack. |
| `stoneChunkDef` / `stoneChunkCount` | `ChunkGranite` / `2` | Dropped when a drill action yields nothing. |
| `allowAnyDeepdrillResource` | `true` | Use every resource with `deepCommonality` above 0, weighted by that commonality. |
| `isInvertWhitelist` | `false` | With `allowAnyDeepdrillResource`, turns `drillableResources` into a blacklist. |
| `drillableResources` | none | Extra resources (or the only ones, if `allowAnyDeepdrillResource` is false). |

Each `drillableResources` entry takes `thingDef`, `amount` (a range such as `25~50`) and `weight` (default `1`). Resources from the deep-drill pool roll `deepCountPerCell` times 0.5 to 1.5 instead.

Resource weights shift with depth. Near `minDepth` common resources dominate, halfway down every resource is equally likely, and near `maxDepth` the weights invert so rare resources come up most.

`baseWorkAmount`, `isFixedWorkAmount`, `selectResourceUiIcon` and the per-entry `fixedWorkAmount` and `workAmount` are left over from the old select-a-resource drill. They still load so old XML doesn't error, but nothing reads them.

## Mod settings

Players get two global multipliers in the mod settings: yield and work amount.

## UI Rethemed look

With [LFS] UI Rethemed - Core active, the drill tab is drawn with its widgets. The code is a separate assembly in `Source/UIRethemedCompat`, compiled against `UIRethemedCore.dll` and loaded only through `LoadFolders.xml` (`IfModActive`), so there is no hard dependency. If the themed draw throws, the tab falls back to the vanilla look and logs one warning.

Build both assemblies with `dotnet build Source/UIRethemedCompat/CVN_EnhancedResourceDrill.UIRethemed.csproj -c Release`. It needs the Core mod from the Workshop (3832843406) or `-p:LfsCoreDir=<path>`.

## Credits

- Design and idea: Bambaryla
- Implementation: Shun The Witch, Sandwish, Taranchuk (basic drill); grid system by Bambaryla
- Inspiration: Quarry and Omni Core Drill
