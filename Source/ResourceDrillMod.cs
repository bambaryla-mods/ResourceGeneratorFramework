using HarmonyLib;
using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill;

public class ResourceDrillMod : Mod
{
    public static ResourceDrillSetting settings = null!;

    public ResourceDrillMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<ResourceDrillSetting>();
        new Harmony("CVN.EnhancedResourceDrill").PatchAll();
    }
    
    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        settings.DoSettingsWindowContents(inRect);
    }
    
    public override string SettingsCategory()
    {
        return "Resource Drill settings";
    }

}

public class ResourceDrillSetting : ModSettings
{
    public float yieldMul = 1f;
    
    public string yieldString = "1.00";

    public float workAmountMul = 1f;
    
    public string workAmountString = "1.00";

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing_Standard = new Listing_Standard();
        listing_Standard.Begin(inRect);
        listing_Standard.Gap(8f);
        listing_Standard.GapLine();
        listing_Standard.Label($"Yield Multiplier: {yieldString}x");
        yieldString = yieldMul.ToString("0.00");
        listing_Standard.TextFieldNumeric(ref yieldMul,ref yieldString,0.01f);
        listing_Standard.Label($"Work Multiplier: {workAmountString}x");
        workAmountString = workAmountMul.ToString("0.00");
        listing_Standard.TextFieldNumeric(ref workAmountMul,ref workAmountString,0.01f);
        listing_Standard.End();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref yieldMul, "yieldMul",1f);
        Scribe_Values.Look(ref workAmountMul, "workAmountMul",1f);
    }
}