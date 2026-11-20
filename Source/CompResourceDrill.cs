using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CVN_EnhancedResourceDrill
{
    public enum DrillAction
    {
        None,
        Left,
        Right,
        Up,
        Down,
        Drill,
        DrillColumn,
        DrillRow,
        DrillCell
    }

    public class CompResourceDrill : ThingComp
    {
        public CompProperties_ResourceDrill Props => (CompProperties_ResourceDrill)props;

        public int Depth;

        public DrillAction PendingAction = DrillAction.None;

        public int ActionWorkDone;

        private float workRemainder;

        public List<DrillAction> Queue = new List<DrillAction>();

        public int PendingTarget;

        public List<int> QueueTargets = new List<int>();

        public CompPowerTrader? powerComp;

        private List<GridCell>? grid;

        private Dictionary<Pawn, IntVec3> workers = new Dictionary<Pawn, IntVec3>();
        private List<Pawn>? pawnKeys;
        private List<IntVec3>? cellsValues;

        public IReadOnlyList<GridCell> Grid
        {
            get
            {
                EnsureGrid();
                return grid!;
            }
        }

        public bool HasPower => powerComp?.PowerOn != false;

        public bool CanOperate
        {
            get
            {
                if (!HasPower || PendingAction == DrillAction.None)
                {
                    return false;
                }
                return Props.isAutomatic || parent.Faction == Faction.OfPlayer;
            }
        }

        public Dictionary<Pawn, IntVec3> Workers
        {
            get
            {
                workers.RemoveAll(x => 
                    x.Key.DestroyedOrNull() 
                    || x.Key.Dead 
                    || x.Key.CurJobDef != Props.operateJobDef 
                    || x.Key.CurJob?.targetA.Thing != this.parent);
                return workers;
            }
        }

        public int DownCostCount
        {
            get
            {
                if (Props.expandDownCostDef == null)
                {
                    return 0;
                }
                return Mathf.Max(0, Props.expandDownCostCount + Props.expandDownCostCountPerDepth * Depth);
            }
        }

        public float CurrentPowerDraw => PowerDrawAtDepth(powerComp?.Props.PowerConsumption ?? 0f, Props, Depth);

        public static float PowerDrawAtDepth(float basePower, CompProperties_ResourceDrill props, int depth)
        {
            int levels = Mathf.Max(0, depth - 1);
            return basePower + props.powerPerDepth * levels;
        }

        public static int StartDepth(CompProperties_ResourceDrill props)
        {
            return Mathf.Clamp(props.startDepth, Mathf.Max(1, props.minDepth), Mathf.Max(1, props.maxDepth));
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.TryGetComp<CompPowerTrader>();
            if (Depth < 1)
            {
                Depth = StartDepth(Props);
            }
            EnsureGrid();
                    }

                    public override void PostExposeData()
                    {
                        base.PostExposeData();
            Scribe_Values.Look(ref Depth, "Depth");
            Scribe_Values.Look(ref PendingAction, "PendingAction", DrillAction.None);
            Scribe_Values.Look(ref ActionWorkDone, "ActionWorkDone");
            Scribe_Collections.Look(ref Queue, "Queue", LookMode.Value);
                        Scribe_Values.Look(ref PendingTarget, "PendingTarget");
                        Scribe_Collections.Look(ref QueueTargets, "QueueTargets", LookMode.Value);
            Scribe_Collections.Look(ref grid, "grid", LookMode.Deep);
            Scribe_Collections.Look(ref workers, "workers", LookMode.Reference, LookMode.Value, ref pawnKeys, ref cellsValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                workers ??= new Dictionary<Pawn, IntVec3>();
                Queue ??= new List<DrillAction>();
                                QueueTargets ??= new List<int>();
                                while (QueueTargets.Count < Queue.Count)
                                {
                                    QueueTargets.Add(0);
                                }
            }
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            ApplyPower();
            if (Props.isAutomatic && parent.IsHashIntervalTick(Props.tickInterval, delta) && CanOperate)
            {
                DoActionWork(null, Props.workPerInterval * delta);
            }
        }

        public void RegisterWorker(Pawn worker)
        {
            var freeSpot = parent.InteractionCells.Where(x => !Workers.Values.Contains(x)).RandomElement();
            Workers[worker] = freeSpot;
        }

        public int WorkRequired(DrillAction action)
        {
            float work = action switch
            {
                DrillAction.Left => Props.workMoveSideways,
                DrillAction.Right => Props.workMoveSideways,
                DrillAction.Up => Props.workMoveUp,
                DrillAction.Down => Props.workMoveDown,
                DrillAction.Drill => Props.workDrill,
                DrillAction.DrillColumn => Props.workDrill / (float)Props.gridWidth,
                DrillAction.DrillRow => Props.workDrill / (float)Props.gridHeight,
                DrillAction.DrillCell => Props.workDrill / (float)(Props.gridWidth * Props.gridHeight),
                _ => 0
            };
            return Mathf.Max(1, Mathf.RoundToInt(work * Mathf.Max(1, Depth) * ResourceDrillMod.settings.workAmountMul));
        }

        public bool CanRequest(DrillAction action, out string? reason)
        {
            reason = null;
            if (PendingAction != DrillAction.None)
            {
                reason = "ResourceDrill_ActionInProgress".Translate();
                return false;
            }
            switch (action)
            {
                case DrillAction.Up:
                    if (Depth <= Mathf.Max(1, Props.minDepth))
                    {
                        reason = "ResourceDrill_AtSurface".Translate();
                        return false;
                    }
                    break;
                case DrillAction.Down:
                    if (Depth >= Props.maxDepth)
                    {
                        reason = "ResourceDrill_MaxDepth".Translate();
                        return false;
                    }
                    if (Props.expandDownCostDef != null && CountAvailable(Props.expandDownCostDef) < DownCostCount)
                    {
                        reason = "ResourceDrill_NotEnoughMaterials".Translate(DownCostCount, Props.expandDownCostDef.label);
                        return false;
                    }
                    break;
            }
            return true;
        }

		public const int MaxQueue = 10;

		public int QueueCount => (PendingAction != DrillAction.None ? 1 : 0) + Queue.Count;

		public List<DrillAction> AllQueued()
		{
			var list = new List<DrillAction>();
			if (PendingAction != DrillAction.None)
			{
				list.Add(PendingAction);
			}
			list.AddRange(Queue);
			return list;
		}

		public List<int> AllQueuedTargets()
		{
			var list = new List<int>();
			if (PendingAction != DrillAction.None)
			{
				list.Add(PendingTarget);
			}
			for (int i = 0; i < Queue.Count; i++)
			{
				list.Add(i < QueueTargets.Count ? QueueTargets[i] : 0);
			}
			return list;
		}

		public bool CanQueueUp => ProjectedDepth() > Mathf.Max(1, Props.minDepth);

		public bool CanQueueDown => ProjectedDepth() < Props.maxDepth;

		private int ProjectedDepth()
		{
			int depth = Depth;
			foreach (var queued in AllQueued())
			{
				if (queued == DrillAction.Up)
				{
					depth--;
				}
				else if (queued == DrillAction.Down)
				{
					depth++;
				}
			}
			return depth;
		}

		public IEnumerable<int> TargetIndices(DrillAction action, int target)
				{
					int w = Props.gridWidth;
					int h = Props.gridHeight;
					switch (action)
					{
						case DrillAction.DrillColumn:
							return Enumerable.Range(0, h).Select(y => y * w + Mathf.Clamp(target, 0, w - 1));
						case DrillAction.DrillRow:
							return Enumerable.Range(0, w).Select(x => Mathf.Clamp(target, 0, h - 1) * w + x);
						case DrillAction.DrillCell:
							return new[] { Mathf.Clamp(target, 0, w * h - 1) };
						default:
							return Enumerable.Range(0, w * h);
					}
				}

				private void RemoveQueueAt(int i)
				{
					Queue.RemoveAt(i);
					if (i < QueueTargets.Count)
					{
						QueueTargets.RemoveAt(i);
					}
				}

				public void RequestAction(DrillAction action, int target = 0)
		{
			if (action == DrillAction.None || QueueCount >= MaxQueue)
			{
				return;
			}
			int projected = ProjectedDepth();
			if (action == DrillAction.Up && projected <= Mathf.Max(1, Props.minDepth))
			{
				return;
			}
			if (action == DrillAction.Down && projected >= Props.maxDepth)
			{
				return;
			}
			if (PendingAction == DrillAction.None)
			{
				PendingAction = action;
				PendingTarget = target;
				ActionWorkDone = 0;
			}
			else
			{
				Queue.Add(action);
								QueueTargets.Add(target);
			}
		}

		private void StartNext()
		{
			while (PendingAction == DrillAction.None && Queue.Count > 0)
			{
				DrillAction next = Queue[0];
								int nextTarget = QueueTargets.Count > 0 ? QueueTargets[0] : 0;
				RemoveQueueAt(0);
				if (next == DrillAction.Up && Depth <= Mathf.Max(1, Props.minDepth))
				{
					continue;
				}
				if (next == DrillAction.Down && Depth >= Props.maxDepth)
				{
					continue;
				}
				PendingAction = next;
								PendingTarget = nextTarget;
				ActionWorkDone = 0;
			}
		}

		public void CancelQueued(int index)
		{
			if (index == 0 && PendingAction != DrillAction.None)
			{
				CancelAction();
				StartNext();
			}
			else if (index >= 1 && index - 1 < Queue.Count)
			{
				RemoveQueueAt(index - 1);
			}
		}

		public void CancelAfter(int index)
		{
			if (Queue.Count > index)
			{
				Queue.RemoveRange(index, Queue.Count - index);
								if (QueueTargets.Count > index)
								{
									QueueTargets.RemoveRange(index, QueueTargets.Count - index);
								}
			}
		}

		public void CancelAll()
		{
			Queue.Clear();
			QueueTargets.Clear();
			CancelAction();
		}

        public void CancelAction()
        {
            PendingAction = DrillAction.None;
            ActionWorkDone = 0;
        }

        public void DoActionWork(Pawn? driller, int amount = 1)
        {
            if (PendingAction == DrillAction.None)
            {
                return;
            }
            if (driller != null)
            {
                workRemainder += amount * Mathf.Max(0.05f, driller.GetStatValue(StatDefOf.DeepDrillingSpeed, cacheStaleAfterTicks: 600));
                int whole = Mathf.FloorToInt(workRemainder);
                workRemainder -= whole;
                ActionWorkDone += whole;
            }
            else
            {
                ActionWorkDone += amount;
            }
            if (ActionWorkDone >= WorkRequired(PendingAction))
            {
                CompleteAction();
            }
        }

        private void CompleteAction()
        {
            CompleteActionInner();
            StartNext();
        }

        private void CompleteActionInner()
        {
            var action = PendingAction;
            int target = PendingTarget;
            CancelAction();
            switch (action)
            {
                case DrillAction.Left:
                    grid = DrillGrid.Shift(Grid.ToList(), Props, Depth, -1); DamageFromMove();
                    break;
                case DrillAction.Right:
                    grid = DrillGrid.Shift(Grid.ToList(), Props, Depth, 1); DamageFromMove();
                    break;
                case DrillAction.Up:
                    Depth = Mathf.Max(1, Depth - 1);
                    grid = DrillGrid.CreateGrid(Props, Depth);
                    ApplyPower();
                    break;
                case DrillAction.Down:
                    if (!TryPayDownCost())
                    {
                        Messages.Message("ResourceDrill_NotEnoughMaterials".Translate(DownCostCount, Props.expandDownCostDef?.label ?? string.Empty), parent, MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    Depth++;
                    grid = DrillGrid.CreateGrid(Props, Depth);
                    ApplyPower();
                    break;
                case DrillAction.Drill:
                case DrillAction.DrillColumn:
                case DrillAction.DrillRow:
                case DrillAction.DrillCell:
                    var indices = TargetIndices(action, target).ToList();
                    var fullGrid = Grid.ToList();
                    var snapshot = indices.Select(i => fullGrid[i]).ToList();
                    SpawnGridContents(snapshot);
                    foreach (int i in indices)
                    {
                        fullGrid[i] = new GridCell();
                    }
                    grid = fullGrid;
                    ResolveHazards(snapshot);
                    break;
            }
        }

        private void DamageFromMove()
        {
            if (!parent.Spawned || parent.Destroyed)
            {
                return;
            }
            float damage = parent.MaxHitPoints * (Depth / 2f) / 100f;
            if (damage > 0f)
            {
                parent.TakeDamage(new DamageInfo(DamageDefOf.Crush, damage));
            }
        }

        private void EnsureGrid()
        {
            if (grid == null || grid.Count != Props.gridWidth * Props.gridHeight)
            {
                grid = DrillGrid.CreateGrid(Props, Depth);
            }
        }

        private void ApplyPower()
        {
            if (powerComp != null && powerComp.PowerOutput < 0f)
            {
                powerComp.PowerOutput = -CurrentPowerDraw;
            }
        }

        private int CountAvailable(ThingDef def)
        {
            if (!parent.Spawned)
            {
                return 0;
            }
            return parent.Map.listerThings.ThingsOfDef(def).Where(t => !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount);
        }

        private bool TryPayDownCost()
        {
            var def = Props.expandDownCostDef;
            int needed = DownCostCount;
            if (def == null || needed <= 0)
            {
                return true;
            }
            if (CountAvailable(def) < needed)
            {
                return false;
            }
            foreach (var thing in parent.Map.listerThings.ThingsOfDef(def).Where(t => !t.IsForbidden(Faction.OfPlayer)).ToList())
            {
                int take = Mathf.Min(needed, thing.stackCount);
                thing.SplitOff(take).Destroy();
                needed -= take;
                if (needed <= 0)
                {
                    break;
                }
            }
            return true;
        }

        private void SpawnGridContents(List<GridCell> source)
        {
            var things = new List<Thing>();
            foreach (var cell in source)
            {
                if (!cell.HasResource)
                {
                    continue;
                }
                int remaining = cell.amount;
                while (remaining > 0)
                {
                    Thing thing = ThingMaker.MakeThing(cell.def!);
                    thing.stackCount = Mathf.Min(thing.def.stackLimit, remaining);
                    remaining -= thing.stackCount;
                    things.Add(thing);
                }
            }
            if (things.Count == 0)
            {
                var chunkDef = Props.stoneChunkDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
                if (chunkDef != null)
                {
                    for (int i = 0; i < Props.stoneChunkCount; i++)
                    {
                        things.Add(ThingMaker.MakeThing(chunkDef));
                    }
                }
            }
            var spawnCenter = parent.InteractionCell;
            foreach (var thing in things)
            {
                int piped = parent is ThingWithComps twc ? PipeSystemCompat.TryPipe(twc, thing.def, thing.stackCount) : 0;
                if (piped >= thing.stackCount)
                {
                    thing.Destroy();
                    continue;
                }
                thing.stackCount -= piped;
                if (!GenPlace.TryPlaceThing(thing, spawnCenter, parent.Map, ThingPlaceMode.Near, out _))
                {
                    Log.Warning($"[ResourceDrill] Could not place {thing.LabelCap} near {spawnCenter}.");
                    thing.Destroy();
                }
            }
        }

        private static readonly string[] MechKindNames = { "Mech_Centipede", "Mech_Lancer", "Mech_Scyther", "Mech_Pikeman", "Mech_Constructoid" };

        private void ResolveHazards(List<GridCell> cells)
        {
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }
            IntVec3 center = parent.Position;
            int hives = cells.Where(c => c.hazard == CellHazard.Hive).Sum(c => c.amount);
            int mechs = cells.Where(c => c.hazard == CellHazard.Mechanoid).Sum(c => c.amount);
            int gasCells = cells.Count(c => c.hazard == CellHazard.Gas);

            if (hives > 0)
            {
                InfestationUtility.SpawnTunnels(hives, map, true, true, null, center);
                Find.LetterStack.ReceiveLetter("ResourceDrill_InfestationLabel".Translate(), "ResourceDrill_HivesBreached".Translate(hives), LetterDefOf.ThreatBig, new TargetInfo(center, map));
            }

            if (mechs > 0)
            {
                var kinds = MechKindNames.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k != null).ToList();
                var spawned = new List<Pawn>();
                for (int i = 0; i < mechs && kinds.Count > 0; i++)
                {
                    PawnKindDef kind = i == 0 && kinds[0].defName == "Mech_Centipede" ? kinds[0] : kinds.RandomElement();
                    Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfMechanoids));
                    if (CellFinder.TryFindRandomCellNear(center, map, 8, c => c.Standable(map) && !c.Fogged(map), out IntVec3 cell))
                    {
                        GenSpawn.Spawn(mech, cell, map);
                        spawned.Add(mech);
                    }
                }
                if (spawned.Count > 0)
                {
                    LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, spawned);
                    Find.LetterStack.ReceiveLetter("ResourceDrill_MechChamberLabel".Translate(), "ResourceDrill_MechChamber".Translate(), LetterDefOf.ThreatBig, spawned);
                }
            }

            if (gasCells > 0)
            {
                Find.LetterStack.ReceiveLetter("ResourceDrill_MethaneLabel".Translate(), "ResourceDrill_Methane".Translate(), LetterDefOf.NegativeEvent, new TargetInfo(center, map));
                float radius = Mathf.Min(GenRadial.MaxRadialPatternRadius - 1f, 10f * gasCells * Depth);
                GenExplosion.DoExplosion(center, map, radius, DamageDefOf.Bomb, parent, Depth);
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sB = new StringBuilder();
            sB.Append(base.CompInspectStringExtra());
            sB.AppendLine("ResourceDrill_Depth".Translate(Depth));
            if (PendingAction != DrillAction.None)
            {
                sB.AppendLine("ResourceDrill_ActionProgress".Translate(ActionLabel(PendingAction), ActionWorkDone, WorkRequired(PendingAction)));
            }
            return sB.ToString().TrimEndNewlines();
        }

        public static string ActionLabel(DrillAction action)
        {
            return ("ResourceDrill_Action_" + action).Translate();
        }
    }
}
