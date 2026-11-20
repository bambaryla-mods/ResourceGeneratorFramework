using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill.UIRethemed;

// Draws the drill tab with [LFS] UI Rethemed - Core's widgets so it matches the themed inspector tabs: rounded window,
// hex title bar, stat strip, rounded grid cells, themed buttons and queue cards. Mirrors how UI Rethemed - Inspector Tabs
// frames its own tabs (vanilla window background off; themed background, title bar, border and close button over it).
// Same content and clicks as the vanilla tab. This assembly is only loaded when the theme is active, and
// ITab_ResourceDrill falls back to the vanilla look if anything here throws.
[StaticConstructorOnStartup]
public static class UIRethemedTab
{
	const int TabWindowId = 235086; // InspectTabBase.DoTabGUI's ImmediateWindow
	const float TitleHeight = 26f;
	const float Pad = 10f;
	const float StripHeight = 28f;
	const float ArrowSize = 34f;
	// Everything in this layout but the grid: title, stats strip, arrows, progress bar, buttons, queue and padding.
	const float FixedHeight = 376f;
	const float QueueBoxSize = 56f;
	const float QueueBoxGap = 4f;
	const int QueueColumns = 5;
	// Not a const or field: nothing typed from UI Rethemed may sit in a field, as this assembly's types are listed
	// while mods load, possibly before UI Rethemed is (mod order).
	static Corners WindowCorners => Corners.Top | Corners.BottomRight;

	static readonly Texture2D TabIcon = CoreWidgets.Icon("mountains");
	static readonly Texture2D DepthIcon = CoreWidgets.Icon("arrow-line-down");
	static readonly Texture2D PowerIcon = CoreWidgets.Icon("lightning");
	static readonly Texture2D WorkersIcon = CoreWidgets.Icon("users");
	static readonly Texture2D UpIcon = CoreWidgets.Icon("arrow-up");
	static readonly Texture2D DownIcon = CoreWidgets.Icon("arrow-down");
	static readonly Texture2D LeftIcon = CoreWidgets.Icon("caret-left");
	static readonly Texture2D RightIcon = CoreWidgets.Icon("caret-right");
	static readonly Texture2D DrillIcon = CoreWidgets.Icon("hammer");
	static readonly Texture2D DrillAllIcon = CoreWidgets.Icon("squares-four");

	static bool drawingOurs;
	static ITab_ResourceDrill shownTab;
	static int shownFrame = -1;
	static float shownSince;

	static Color MoveAccent => Theme.HighlightBlue;
	static Color DrillAccent => Theme.HighlightRed;

	static UIRethemedTab()
	{
		try
		{
			var harmony = new Harmony("bambaryla.resourcegeneratorframework.uirethemed");
			harmony.Patch(AccessTools.Method(typeof(InspectTabBase), nameof(InspectTabBase.DoTabGUI)),
				prefix: new HarmonyMethod(typeof(UIRethemedTab), nameof(DoTabGUIPrefix)),
				finalizer: new HarmonyMethod(typeof(UIRethemedTab), nameof(DoTabGUIFinalizer)));
			harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.ImmediateWindow)),
				prefix: new HarmonyMethod(typeof(UIRethemedTab), nameof(ImmediateWindowPrefix)));
			ITab_ResourceDrill.ThemedFill = Fill;
			Log.Message("[ResourceGeneratorFramework] UI Rethemed detected: the drill tab uses its theme.");
		}
		catch (Exception e)
		{
			Log.Warning($"[ResourceGeneratorFramework] Could not hook into UI Rethemed, the drill tab keeps the vanilla look: {e}");
		}
	}

	static void DoTabGUIPrefix(InspectTabBase __instance) =>
		drawingOurs = __instance is ITab_ResourceDrill && ITab_ResourceDrill.ThemedFill != null;

	static void DoTabGUIFinalizer() => drawingOurs = false;

	static void ImmediateWindowPrefix(int ID, ref bool doBackground)
	{
		if (ID == TabWindowId && drawingOurs) doBackground = false;
	}

	static void Fill(ITab_ResourceDrill tab, Rect rect)
	{
		var comp = tab.compResourceDrill;
		var closeRect = new Rect(rect.width - 22f, 4f, 18f, 18f); // vanilla's close button sits here and takes the click
		try
		{
			Rounded.WindowBackground(rect, WindowCorners);
			TitleBar(new Rect(0f, 0f, rect.width, TitleHeight), closeRect.x - 4f, Age(tab), tab.drillThing.LabelCap);
			if (comp == null) return;

			var body = new Rect(Pad, TitleHeight + 8f, rect.width - Pad * 2f, rect.height - TitleHeight - 8f - Pad);
			float y = body.y;
			Stats(new Rect(body.x, y, body.width, StripHeight), tab);
			y += StripHeight + 8f;
			y = GridWithArrows(tab, body, y, out var gridRect);
			y = ActionArea(tab, body, y, gridRect);
			Queue(comp, body, y);
		}
		finally
		{
			Theme.CloseScrolls();
			Theme.Reset();
		}
		Rounded.Border(rect, Rounded.WindowBorderColor, WindowCorners);
		CoreWidgets.IconButton(closeRect, CoreWidgets.CloseTex, "CloseButton".Translate(), iconSize: 14f);
		Theme.Reset();
	}

	// Same title bar as the themed pawn tabs: dark top, fading hex pattern, an accent line that grows when the tab opens.
	static void TitleBar(Rect bar, float right, float age, string title)
	{
		Rounded.Fill(bar, Theme.BGDark, Corners.Top);
		Faces.FadingHex(bar, Theme.HighlightGreen, Theme.BGDark, 1f, 0.6f);
		CoreWidgets.Accent(bar, Theme.HighlightGreen, ThemeMotion.On ? age : 1f);

		float x = bar.x + 10f;
		CoreWidgets.DrawIcon(new Rect(x, Mathf.Round(bar.center.y - 9f), 18f, 18f), TabIcon);
		x += 26f;
		CoreWidgets.Line(new Rect(x, bar.y, Mathf.Max(0f, right - x), bar.height), title, Color.white);
	}

	// Depth, power draw and workers as the theme's centred stat strip; the full sentence is in each stat's hover card.
	static void Stats(Rect room, ITab_ResourceDrill tab)
	{
		var comp = tab.compResourceDrill;
		int workers = comp.Workers.Count;
		var stats = new List<SummaryStat>
		{
			new(DepthIcon, comp.Depth.ToString(), MoveAccent, true, "ResourceDrill_Depth".Translate(comp.Depth), null),
			new(PowerIcon, comp.CurrentPowerDraw.ToString("F0") + " W", Theme.HighlightYellow, true,
				"ResourceDrill_PowerDraw".Translate(comp.CurrentPowerDraw.ToString("F0")), null),
			new(WorkersIcon, $"{workers} / {tab.drillThing.InteractionCells.Count}", Theme.HighlightGreen, workers > 0,
				"ResourceDrill_Workers".Translate(workers, tab.drillThing.InteractionCells.Count), null),
		};
		// SummaryStrip right-aligns to its room; give it a room exactly as wide as the strip so it ends up centred.
		float width = stats.Sum(s => 25f + CoreWidgets.Width(s.text) + 12f);
		width = Mathf.Min(Mathf.Ceil(width), room.width);
		CoreWidgets.SummaryStrip(new Rect(room.center.x - width / 2f, room.y, width, room.height), stats);
	}

	static float GridWithArrows(ITab_ResourceDrill tab, Rect body, float y, out Rect gridRect)
	{
		var comp = tab.compResourceDrill;
		var props = comp.Props;
		float availableWidth = body.width - 2f * (ArrowSize + 6f);
		float cellSize = Mathf.Floor(Mathf.Min(availableWidth / props.gridWidth, tab.GridMaxHeight(FixedHeight) / props.gridHeight));
		float gridWidth = cellSize * props.gridWidth;
		float gridHeight = cellSize * props.gridHeight;

		Arrow(tab, new Rect(body.center.x - ArrowSize / 2f, y, ArrowSize, ArrowSize), UpIcon, DrillAction.Up, comp.CanQueueUp);
		y += ArrowSize + 4f;

		gridRect = new Rect(body.center.x - gridWidth / 2f, y, gridWidth, gridHeight);
		Grid(comp, gridRect, cellSize);

		if (props.preciseDrilling == 1)
		{
			float offset = (cellSize - ArrowSize) / 2f;
			PreciseButton(tab, new Rect(gridRect.xMin + offset, gridRect.yMin - ArrowSize - 4f, ArrowSize, ArrowSize), DownIcon,
				DrillAction.DrillColumn, new Rect(gridRect.xMin, gridRect.yMin, cellSize, gridRect.height));
			PreciseButton(tab, new Rect(gridRect.xMin - ArrowSize - 6f, gridRect.yMin + offset, ArrowSize, ArrowSize), RightIcon,
				DrillAction.DrillRow, new Rect(gridRect.xMin, gridRect.yMin, gridRect.width, cellSize));
		}

		float middleY = gridRect.center.y - ArrowSize / 2f;
		Arrow(tab, new Rect(gridRect.xMin - ArrowSize - 6f, middleY, ArrowSize, ArrowSize), LeftIcon, DrillAction.Left, true);
		Arrow(tab, new Rect(gridRect.xMax + 6f, middleY, ArrowSize, ArrowSize), RightIcon, DrillAction.Right, true);

		y = gridRect.yMax + 4f;
		Arrow(tab, new Rect(body.center.x - ArrowSize / 2f, y, ArrowSize, ArrowSize), DownIcon, DrillAction.Down, comp.CanQueueDown);
		return y + ArrowSize + 8f;
	}

	static void Grid(CompResourceDrill comp, Rect gridRect, float cellSize)
	{
		var props = comp.Props;
		var cells = comp.Grid;
		var frame = gridRect.ExpandedBy(3f);
		Rounded.Fill(frame, Theme.BGDark);
		Rounded.Border(frame, Theme.BGLight);

		for (int gy = 0; gy < props.gridHeight; gy++)
		{
			for (int gx = 0; gx < props.gridWidth; gx++)
			{
				int index = gy * props.gridWidth + gx;
				var cellRect = new Rect(gridRect.x + gx * cellSize, gridRect.y + gy * cellSize, cellSize, cellSize);
				var face = cellRect.ContractedBy(1.5f);
				var cell = cells[index];

				if (cell.unknown)
				{
					Rounded.Fill(face, Theme.BGRed);
					Theme.Hatch(face, Faces.Fade(Theme.HighlightRed, 0.35f));
					CoreWidgets.Tip(cellRect, "ResourceDrill_Tip_Unknown".Translate(), accent: Theme.HighlightRed);
				}
				else if (cell.hazard != CellHazard.None)
				{
					Rounded.Fill(face, Theme.BGYellow);
					ITab_ResourceDrill.DrawHazardIcon(cellRect.ContractedBy(2f), cell.hazard);
					if (cell.hazard == CellHazard.Gas)
						CoreWidgets.Tip(cellRect, "ResourceDrill_Tip_Methane".Translate(), accent: Theme.HighlightYellow);
					else if (cell.hazard == CellHazard.Hive)
						CoreWidgets.Tip(cellRect, "ResourceDrill_Tip_Hive".Translate(), accent: Theme.HighlightYellow);
				}
				else
				{
					Rounded.Fill(face, Theme.BG);
					if (cell.HasResource)
					{
						ITab_ResourceDrill.DrawResourceIcons(cellRect.ContractedBy(2f), cell, index);
						CoreWidgets.Tip(cellRect, cell.def.LabelCap);
					}
				}

				if (props.preciseDrilling >= 2)
				{
					if (Mouse.IsOver(cellRect)) Rounded.Fill(face, Faces.Fade(DrillAccent, 0.3f));
					if (Widgets.ButtonInvisible(cellRect, false)) comp.RequestAction(DrillAction.DrillCell, index);
				}
			}
		}
	}

	static void Arrow(ITab_ResourceDrill tab, Rect rect, Texture2D icon, DrillAction action, bool enabled)
	{
		if (CoreWidgets.Button(rect, null, icon, MoveAccent, enabled, tab.DescribeAction(action)) && enabled)
			tab.compResourceDrill.RequestAction(action);
	}

	static void PreciseButton(ITab_ResourceDrill tab, Rect rect, Texture2D icon, DrillAction action, Rect highlight)
	{
		if (Mouse.IsOver(rect)) Rounded.Fill(highlight, Faces.Fade(DrillAccent, 0.3f));
		if (CoreWidgets.Button(rect, null, icon, DrillAccent, true, tab.DescribeAction(action)))
			tab.compResourceDrill.RequestAction(action);
	}

	static float ActionArea(ITab_ResourceDrill tab, Rect body, float y, Rect gridRect)
	{
		var comp = tab.compResourceDrill;
		var action = comp.PendingAction;
		var barRect = new Rect(body.x, y, body.width, 24f);
		if (action != DrillAction.None)
		{
			int required = comp.WorkRequired(action);
			Theme.Bar(barRect, required <= 0 ? 1f : Mathf.Clamp01((float)comp.ActionWorkDone / required),
				action >= DrillAction.Drill ? DrillAccent : MoveAccent);
			Theme.Label(barRect, "ResourceDrill_ActionProgress".Translate(CompResourceDrill.ActionLabel(action), comp.ActionWorkDone, required),
				TextAnchor.MiddleCenter, Color.white);
		}
		else
		{
			Theme.Bar(barRect, 0f, MoveAccent);
			Theme.Label(barRect, CompResourceDrill.ActionLabel(DrillAction.None), TextAnchor.MiddleCenter, Theme.TextFaint);
		}
		y += 30f;

		float half = (body.width - 6f) / 2f;
		var drillRect = new Rect(body.x, y, half, 30f);
		if (Mouse.IsOver(drillRect)) Rounded.Fill(gridRect, Faces.Fade(DrillAccent, 0.3f));
		if (CoreWidgets.Button(drillRect, "ResourceDrill_DrillAll".Translate(), DrillIcon, DrillAccent,
			tip: tab.DescribeAction(DrillAction.Drill), raised: true))
			comp.RequestAction(DrillAction.Drill);

		bool anyQueued = comp.QueueCount > 0;
		if (CoreWidgets.Button(new Rect(body.x + half + 6f, y, half, 30f), "ResourceDrill_Cancel".Translate(), CoreWidgets.CloseTex,
			Theme.HighlightGray, anyQueued) && anyQueued)
			comp.CancelAll();
		return y + 38f;
	}

	static void Queue(CompResourceDrill comp, Rect body, float y)
	{
		var queued = comp.AllQueued();
		var targets = comp.AllQueuedTargets();
		CoreWidgets.Section(ref y, body.x, body.width, "ResourceDrill_Queue".Translate(), $"{queued.Count} / {CompResourceDrill.MaxQueue}");
		y += 4f;

		float gridWidth = QueueColumns * QueueBoxSize + (QueueColumns - 1) * QueueBoxGap;
		float startX = body.x + (body.width - gridWidth) / 2f;
		bool hasPending = comp.PendingAction != DrillAction.None;
		for (int i = 0; i < CompResourceDrill.MaxQueue; i++)
		{
			var box = new Rect(startX + i % QueueColumns * (QueueBoxSize + QueueBoxGap),
				y + i / QueueColumns * (QueueBoxSize + QueueBoxGap), QueueBoxSize, QueueBoxSize);
			if (i >= queued.Count)
			{
				Rounded.Fill(box, Faces.Fade(Theme.BGDark, 0.6f));
				Rounded.Border(box, Faces.Fade(Theme.BGLight, 0.5f));
				continue;
			}

			var a = queued[i];
			var accent = a >= DrillAction.Drill ? DrillAccent : MoveAccent;
			bool active = i == 0 && hasPending;
			CoreWidgets.Card(box, active, accent);
			if (!active) Rounded.Border(box, Faces.Fade(accent, 0.7f));

			var icon = a switch
			{
				DrillAction.Left => LeftIcon,
				DrillAction.Right => RightIcon,
				DrillAction.Up => UpIcon,
				DrillAction.Down => DownIcon,
				DrillAction.Drill => DrillAllIcon,
				_ => null
			};
			if (icon != null) CoreWidgets.CenteredIcon(box, icon, 26f);

			int t = i < targets.Count ? targets[i] : 0;
			string label = a switch
			{
				DrillAction.DrillRow => "R" + (t + 1),
				DrillAction.DrillColumn => "C" + (t + 1),
				DrillAction.DrillCell => (t % comp.Props.gridWidth + 1) + "," + (t / comp.Props.gridWidth + 1),
				_ => null
			};
			if (label != null) Theme.Label(box, label, TextAnchor.MiddleCenter, Color.white, GameFont.Medium);

			CoreWidgets.Tip(box, null, "ResourceDrill_QueueTip".Translate(CompResourceDrill.ActionLabel(a)), accent: accent);
			if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(box))
			{
				comp.CancelAfter(hasPending ? i : i + 1);
				Event.current.Use();
			}
			else if (Widgets.ButtonInvisible(box, false))
			{
				comp.CancelQueued(i);
			}
		}
	}

	// Seconds since the tab was last shown, for the title accent's grow-in.
	static float Age(ITab_ResourceDrill tab)
	{
		if (tab != shownTab || Time.frameCount - shownFrame > 2)
		{
			shownTab = tab;
			shownSince = ThemeMotion.Now;
		}
		shownFrame = Time.frameCount;
		return ThemeMotion.Now - shownSince;
	}
}
