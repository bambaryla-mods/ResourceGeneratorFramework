using RimWorld;
using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill;

public class ITab_ResourceDrill : ITab
{
    private const float Margin = 10f;
    private const float ArrowSize = 34f;
    private const float MaxGridHeight = 250f;
    private const float LineHeight = 22f;

    private const int LargeIconAmount = 1000;
    private const int MediumIconAmount = 100;
    private const int SmallIconAmount = 10;
    private const int MaxIconsPerTier = 12;

    public CompResourceDrill compResourceDrill = null!;

    public Thing drillThing = null!;

    // Set by the optional UI Rethemed compat assembly (Compat/UIRethemed) to draw the whole tab in that theme.
    // Draws the vanilla look while unset; a themed draw that throws is dropped for good and the vanilla look returns.
    public static Action<ITab_ResourceDrill, Rect>? ThemedFill;

    public Vector2 Size => size;

    public ITab_ResourceDrill()
    {
        size = new Vector2(Mathf.Min(420f, UI.screenWidth), Mathf.Min(680f, UI.screenHeight));
        labelKey = "ITab_ResourceDrillKeyed";
    }

    private const float DesiredHeight = 680f;
    private const float MinHeight = 320f;
    // Everything in the vanilla layout but the grid: margins, header, arrows, progress bar, buttons, queue.
    private const float VanillaFixedHeight = 388f;

    // Never taller than the room above the inspect pane: with display scaling (UI height = screen / scale) or a tall
    // pane, a fixed 680 put the top of the tab off screen. The grid shrinks to fit (GridMaxHeight).
    protected override void UpdateSize()
    {
        base.UpdateSize();
        size = new Vector2(Mathf.Min(420f, UI.screenWidth), Mathf.Clamp(PaneTopY - 30f - 6f, MinHeight, DesiredHeight));
    }

    // Tallest the grid may be when everything else in the layout takes fixedHeight.
    public float GridMaxHeight(float fixedHeight) => Mathf.Clamp(size.y - fixedHeight, 40f, MaxGridHeight);

    public override void OnOpen()
    {
        base.OnOpen();
        GetDrillInfo();
    }

    public void GetDrillInfo()
    {
        drillThing = SelThing;
        compResourceDrill = drillThing.TryGetComp<CompResourceDrill>();
    }

    protected override void FillTab()
    {
        if (SelThing != drillThing)
        {
            GetDrillInfo();
        }
        if (ThemedFill != null)
        {
            try
            {
                ThemedFill(this, new Rect(0f, 0f, size.x, size.y));
                return;
            }
            catch (Exception e)
            {
                ThemedFill = null;
                Log.Warning($"[ResourceGeneratorFramework] The UI Rethemed look for the drill tab failed, using the vanilla look: {e}");
            }
        }
        Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
        float y = DrawHeader(rect);
        y = DrawGridWithArrows(rect, y);
        DrawActionArea(rect, y);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private float DrawHeader(Rect rect)
    {
        float y = rect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rect.x, y, rect.width, 30f), drillThing.LabelCap);
        Text.Font = GameFont.Small;
        y += 32f;
        Widgets.Label(new Rect(rect.x, y, rect.width, LineHeight), "ResourceDrill_Depth".Translate(compResourceDrill.Depth));
        y += LineHeight;
        Widgets.Label(new Rect(rect.x, y, rect.width, LineHeight), "ResourceDrill_PowerDraw".Translate(compResourceDrill.CurrentPowerDraw.ToString("F0")));
        y += LineHeight;
        Widgets.Label(new Rect(rect.x, y, rect.width, LineHeight), "ResourceDrill_Workers".Translate(compResourceDrill.Workers.Count, drillThing.InteractionCells.Count));
        y += LineHeight + 4f;
        return y;
    }

    private float DrawGridWithArrows(Rect rect, float y)
    {
        var props = compResourceDrill.Props;
        float availableWidth = rect.width - 2f * (ArrowSize + 6f);
        float cellSize = Mathf.Floor(Mathf.Min(availableWidth / props.gridWidth, GridMaxHeight(VanillaFixedHeight) / props.gridHeight));
        float gridWidth = cellSize * props.gridWidth;
        float gridHeight = cellSize * props.gridHeight;

        Rect upRect = new Rect(rect.center.x - ArrowSize / 2f, y, ArrowSize, ArrowSize);
        if (compResourceDrill.CanQueueUp)
        {
            DrawArrow(upRect, TexButton.ReorderUp, DrillAction.Up);
        }
        y += ArrowSize + 4f;

        Rect gridRect = new Rect(rect.center.x - gridWidth / 2f, y, gridWidth, gridHeight);
        lastGridRect = gridRect;
        DrawGrid(gridRect, cellSize);

        if (props.preciseDrilling == 1)
        {
            float offset = (cellSize - ArrowSize) / 2f;
            Rect colButton = new Rect(gridRect.xMin + offset, gridRect.yMin - ArrowSize - 4f, ArrowSize, ArrowSize);
            Rect rowButton = new Rect(gridRect.xMin - ArrowSize - 6f, gridRect.yMin + offset, ArrowSize, ArrowSize);
            DrawPreciseButton(colButton, TexButton.ReorderDown, DrillAction.DrillColumn, new Rect(gridRect.xMin, gridRect.yMin, cellSize, gridRect.height));
            DrawPreciseButton(rowButton, TexUI.ArrowTexRight, DrillAction.DrillRow, new Rect(gridRect.xMin, gridRect.yMin, gridRect.width, cellSize));
        }

        float middleY = gridRect.center.y - ArrowSize / 2f;
        DrawArrow(new Rect(gridRect.xMin - ArrowSize - 6f, middleY, ArrowSize, ArrowSize), TexUI.ArrowTexLeft, DrillAction.Left);
        DrawArrow(new Rect(gridRect.xMax + 6f, middleY, ArrowSize, ArrowSize), TexUI.ArrowTexRight, DrillAction.Right);

        y = gridRect.yMax + 4f;
        if (compResourceDrill.CanQueueDown)
        {
            DrawArrow(new Rect(rect.center.x - ArrowSize / 2f, y, ArrowSize, ArrowSize), TexButton.ReorderDown, DrillAction.Down);
        }
        return y + ArrowSize + 8f;
    }

    private void DrawGrid(Rect gridRect, float cellSize)
    {
        var props = compResourceDrill.Props;
        var cells = compResourceDrill.Grid;
        Widgets.DrawBoxSolid(gridRect, Color.black);
        for (int gy = 0; gy < props.gridHeight; gy++)
        {
            for (int gx = 0; gx < props.gridWidth; gx++)
            {
                int index = gy * props.gridWidth + gx;
                Rect cellRect = new Rect(gridRect.x + gx * cellSize, gridRect.y + gy * cellSize, cellSize, cellSize);
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                Widgets.DrawBox(cellRect);
                GUI.color = Color.white;
                var cell = cells[index];
                if (props.preciseDrilling >= 2)
                {
                    if (Mouse.IsOver(cellRect))
                    {
                        Widgets.DrawBoxSolid(cellRect.ContractedBy(1f), new Color(0.8f, 0.1f, 0.1f, 0.25f));
                    }
                    if (Widgets.ButtonInvisible(cellRect, false))
                    {
                        compResourceDrill.RequestAction(DrillAction.DrillCell, index);
                    }
                }
                if (cell.unknown)
                {
                    Widgets.DrawBoxSolid(cellRect.ContractedBy(1f), new Color(0.8f, 0.1f, 0.1f, 0.45f));
                    TooltipHandler.TipRegion(cellRect, "ResourceDrill_Tip_Unknown".Translate());
                }
                else if (cell.hazard != CellHazard.None)
                {
                    DrawHazardIcon(cellRect.ContractedBy(2f), cell.hazard);
                    if (cell.hazard == CellHazard.Gas)
                    {
                        TooltipHandler.TipRegion(cellRect, "ResourceDrill_Tip_Methane".Translate());
                    }
                    else if (cell.hazard == CellHazard.Hive)
                    {
                        TooltipHandler.TipRegion(cellRect, "ResourceDrill_Tip_Hive".Translate());
                    }
                }
                else if (cell.HasResource)
                {
                    DrawResourceIcons(cellRect.ContractedBy(2f), cell, index);
                    TooltipHandler.TipRegion(cellRect, cell.def!.LabelCap);
                }
            }
        }
        GUI.color = new Color(0.21f, 0.48f, 0.44f);
        Widgets.DrawBox(gridRect);
        GUI.color = Color.white;
    }

    private static ThingDef? mechIconRace;
    private static bool mechIconResolved;

    private static ThingDef? MechIconRace()
    {
        if (!mechIconResolved)
        {
            mechIconResolved = true;
            foreach (string name in new[] { "Mech_CentipedeBlaster", "Mech_CentipedeGunner", "Mech_CentipedeBurner", "Mech_Lancer" })
            {
                mechIconRace = DefDatabase<PawnKindDef>.GetNamedSilentFail(name)?.race;
                if (mechIconRace != null)
                {
                    break;
                }
            }
        }
        return mechIconRace;
    }

    public static void DrawHazardIcon(Rect rect, CellHazard hazard)
    {
        switch (hazard)
        {
            case CellHazard.Hive:
                Widgets.ThingIcon(rect, ThingDefOf.Hive);
                break;
            case CellHazard.Mechanoid:
                ThingDef? mechRace = MechIconRace();
                if (mechRace != null)
                {
                    Widgets.ThingIcon(rect, mechRace);
                }
                break;
            case CellHazard.Gas:
                float size = Mathf.Min(rect.width, rect.height) * 0.5f;
                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(rect.center.x - size / 2f, rect.center.y - size / 2f, size, size), LetterDefOf.NegativeEvent.Icon);
                GUI.color = Color.white;
                break;
        }
    }

    public static void DrawResourceIcons(Rect cellRect, GridCell cell, int index)
    {
        ThingDef def = cell.def!;
        float minSide = Mathf.Min(cellRect.width, cellRect.height);
        Rand.PushState(index * 7919 + def.shortHash);
        try
        {
            ScatterIcons(cellRect, def, cell.amount / LargeIconAmount, minSide * 0.5f);
            ScatterIcons(cellRect, def, cell.amount % LargeIconAmount / MediumIconAmount, minSide * 0.32f);
            ScatterIcons(cellRect, def, cell.amount % MediumIconAmount / SmallIconAmount, minSide * 0.18f);
        }
        finally
        {
            Rand.PopState();
        }
    }

    private static void ScatterIcons(Rect cellRect, ThingDef def, int count, float iconSize)
    {
        count = Mathf.Min(count, MaxIconsPerTier);
        for (int i = 0; i < count; i++)
        {
            Rect iconRect = new Rect(
                cellRect.x + Rand.Range(0f, Mathf.Max(0f, cellRect.width - iconSize)),
                cellRect.y + Rand.Range(0f, Mathf.Max(0f, cellRect.height - iconSize)),
                iconSize,
                iconSize);
            Widgets.ThingIcon(iconRect, def);
        }
    }

	private void DrawPreciseButton(Rect rect, Texture2D texture, DrillAction action, Rect highlight)
	{
		TooltipHandler.TipRegion(rect, DescribeAction(action));
		if (Mouse.IsOver(rect))
		{
			Widgets.DrawBoxSolid(highlight, new Color(0.8f, 0.1f, 0.1f, 0.25f));
		}
		if (Widgets.ButtonImage(rect, texture))
		{
			compResourceDrill.RequestAction(action);
		}
	}

	private void DrawArrow(Rect rect, Texture2D texture, DrillAction action)
	{
		TooltipHandler.TipRegion(rect, DescribeAction(action));
		if (Widgets.ButtonImage(rect, texture))
		{
			compResourceDrill.RequestAction(action);
		}
	}

	public string DescribeAction(DrillAction action)
	{
		string text = "ResourceDrill_ActionTip".Translate(CompResourceDrill.ActionLabel(action), compResourceDrill.WorkRequired(action));
		if (action == DrillAction.Down)
		{
			var costDef = compResourceDrill.Props.expandDownCostDef;
			if (costDef != null && compResourceDrill.DownCostCount > 0)
			{
				text += "\n" + "ResourceDrill_Cost".Translate(compResourceDrill.DownCostCount, costDef.label);
			}
		}
		return text;
	}

	private Rect lastGridRect;


	private const float QueueBoxSize = 56f;
	private const float QueueBoxGap = 4f;
	private const int QueueColumns = 5;

	private void DrawActionArea(Rect rect, float y)
	{
		var action = compResourceDrill.PendingAction;
		Rect barRect = new Rect(rect.x, y, rect.width, 24f);
		if (action != DrillAction.None)
		{
			float required = compResourceDrill.WorkRequired(action);
			Widgets.FillableBar(barRect, Mathf.Clamp01(compResourceDrill.ActionWorkDone / required), StaticAssetCached.progressTex, StaticAssetCached.progressBGTex, true);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(barRect, CompResourceDrill.ActionLabel(action));
			Text.Anchor = TextAnchor.UpperLeft;
		}
		y += 30f;

		float half = (rect.width - 6f) / 2f;
		Rect drillRect = new Rect(rect.x, y, half, 30f);
		TooltipHandler.TipRegion(drillRect, DescribeAction(DrillAction.Drill));
		if (Mouse.IsOver(drillRect))
        {
            Widgets.DrawBoxSolid(lastGridRect, new Color(0.8f, 0.1f, 0.1f, 0.25f));
        }
        if (Widgets.ButtonText(drillRect, "ResourceDrill_DrillAll".Translate()))
		{
			compResourceDrill.RequestAction(DrillAction.Drill);
		}
		Rect cancelRect = new Rect(rect.x + half + 6f, y, half, 30f);
		if (Widgets.ButtonText(cancelRect, "ResourceDrill_Cancel".Translate()))
		{
			compResourceDrill.CancelAll();
		}
		y += 36f;

		DrawQueue(new Rect(rect.x, y, rect.width, QueueBoxSize * 2f + QueueBoxGap));
	}

	private void DrawQueue(Rect area)
	{
		var queued = compResourceDrill.AllQueued();
				var queuedTargets = compResourceDrill.AllQueuedTargets();
		float gridWidth = QueueColumns * QueueBoxSize + (QueueColumns - 1) * QueueBoxGap;
		float startX = area.x + (area.width - gridWidth) / 2f;
		for (int i = 0; i < CompResourceDrill.MaxQueue; i++)
		{
			int col = i % QueueColumns;
			int row = i / QueueColumns;
			Rect box = new Rect(startX + col * (QueueBoxSize + QueueBoxGap), area.y + row * (QueueBoxSize + QueueBoxGap), QueueBoxSize, QueueBoxSize);
			if (i >= queued.Count)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.15f);
				Widgets.DrawBox(box);
				GUI.color = Color.white;
				continue;
			}
			DrillAction a = queued[i];
			Widgets.DrawBoxSolid(box, a >= DrillAction.Drill ? new Color(0.7f, 0.1f, 0.1f) : new Color(0.15f, 0.15f, 0.15f));
			if (i == 0)
			{
				GUI.color = Color.yellow;
				Widgets.DrawBox(box);
				GUI.color = Color.white;
			}
			Texture2D? icon = a switch
			{
				DrillAction.Left => TexUI.ArrowTexLeft,
				DrillAction.Right => TexUI.ArrowTexRight,
				DrillAction.Up => TexButton.ReorderUp,
				DrillAction.Down => TexButton.ReorderDown,
				_ => null
			};
			if (icon != null)
			{
				GUI.DrawTexture(box.ContractedBy(10f), icon);
			}
			string? boxLabel = null;
			int t = i < queuedTargets.Count ? queuedTargets[i] : 0;
			switch (a)
			{
				case DrillAction.DrillRow:
					boxLabel = "R" + (t + 1);
					break;
				case DrillAction.DrillColumn:
					boxLabel = "C" + (t + 1);
					break;
				case DrillAction.DrillCell:
					int gw = compResourceDrill.Props.gridWidth;
					boxLabel = (t % gw + 1) + "," + (t / gw + 1);
					break;
			}
			if (boxLabel != null)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Medium;
				Widgets.Label(box, boxLabel);
				Text.Font = GameFont.Small;
				Text.Anchor = TextAnchor.UpperLeft;
			}
			TooltipHandler.TipRegion(box, "ResourceDrill_QueueTip".Translate(CompResourceDrill.ActionLabel(a)));
            bool hasPending = compResourceDrill.PendingAction != DrillAction.None;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(box))
            {
                compResourceDrill.CancelAfter(hasPending ? i : i + 1);
                Event.current.Use();
            }
            else if (Widgets.ButtonInvisible(box, false))
            {
                compResourceDrill.CancelQueued(i);
            }
        }
    }
}
