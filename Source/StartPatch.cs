using Verse;

namespace CVN_EnhancedResourceDrill
{
    [StaticConstructorOnStartup]
    public static class StartPatch
    {
        static StartPatch()
        {
            StaticCollectionCached.InitList();
        }
    }
}