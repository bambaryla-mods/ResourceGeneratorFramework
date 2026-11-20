using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CVN_EnhancedResourceDrill
{
    public class JobDriver_OperateResourceDrill : JobDriver
    {
        private CompResourceDrill? CompCached;
        private CompResourceDrill CompResourceDrill => CompCached ??= job.targetA.Thing.TryGetComp<CompResourceDrill>();
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        
        public override void Notify_Starting()
        {
            base.Notify_Starting();
            CompResourceDrill.RegisterWorker(pawn);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOnThingHavingDesignation(TargetIndex.A, DesignationDefOf.Uninstall);
            this.FailOn(() => !CompResourceDrill.HasPower);
            yield return Toils_Goto.GotoCell(CompResourceDrill.Workers.TryGetValue(pawn, out var cell) ? cell : TargetThingA.InteractionCell, PathEndMode.OnCell);
            Toil work = ToilMaker.MakeToil("MakeNewToils");
            work.tickAction = delegate
            {
                CompResourceDrill.DoActionWork(work.actor);
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.AddEndCondition(() => CompResourceDrill.PendingAction == DrillAction.None ? JobCondition.Succeeded : JobCondition.Ongoing);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return work;
            AddFinishAction(delegate
            {
                CompResourceDrill.Workers.Remove(pawn);
            });
        }
    }
}