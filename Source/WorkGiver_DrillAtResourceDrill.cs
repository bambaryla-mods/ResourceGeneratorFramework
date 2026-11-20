using RimWorld;
using Verse;
using Verse.AI;

namespace CVN_EnhancedResourceDrill
{
    public class WorkGiver_DrillAtResourceDrill : WorkGiver_Scanner
    {
        public DefModExtension_ResourceDrill modExtension => def.GetModExtension<DefModExtension_ResourceDrill>();
        
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(modExtension.DrillThingDef);
        
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction)
            {
                return false;
            }
            Building? building = t as Building;
            if (building == null)
            {
                return false;
            }
            if (building.IsForbidden(pawn))
            {
                return false;
            }
            var comp = building.TryGetComp<CompResourceDrill>();
            if (comp == null || comp.Props.isAutomatic || !comp.CanOperate)
            {
                return false;
            }
            if (comp.Workers.Count >= building.InteractionCells.Count)
            {
                return false;
            }
            if (building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) != null)
            {
                return false;
            }
            if (building.IsBurning())
            {
                return false;
            }
            var power = building.TryGetComp<CompPowerTrader>();
            if (power is { PowerOn: false })
            {
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(modExtension.DrillJobDef, t, 1500, checkOverrideOnExpiry: true);
        }
    }
}