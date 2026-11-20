using UnityEngine;
using Verse;

namespace CVN_EnhancedResourceDrill;

[StaticConstructorOnStartup]
public static class StaticAssetCached
{
    public static Texture2D backgroundTex;
    public static Texture2D backgroundBorderTex;
    public static Texture2D progressBGTex;
    public static Texture2D progressTex;
    public static Texture2D IconBorderTex;
    
    static StaticAssetCached()
    {
        backgroundTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.17f, 0.29f, 0.48f));
        backgroundBorderTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.21f, 0.48f, 0.44f));
        IconBorderTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.37f, 0.37f, 0.37f));
        progressBGTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f));
        progressTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.69f, 0f));
    }
}