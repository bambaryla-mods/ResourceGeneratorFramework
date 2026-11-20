using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace CVN_EnhancedResourceDrill
{
    // Soft dependency on Vanilla Expanded Framework's PipeSystem, accessed via reflection only.
    public static class PipeSystemCompat
    {
        private static bool initialized;
        private static Type? compResourceType;
        private static Type? managerType;
        private static PropertyInfo? pipeNetProp;

        private static void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            try
            {
                compResourceType = GenTypes.GetTypeInAnyAssembly("PipeSystem.CompResource");
                managerType = GenTypes.GetTypeInAnyAssembly("PipeSystem.PipeNetManager");
                pipeNetProp = compResourceType?.GetProperty("PipeNet", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Log.Warning($"[ResourceDrill] PipeSystem lookup failed: {ex.Message}");
                compResourceType = null;
            }
        }

        // Returns how many items of the given stack were pushed into a connected pipe net.
        // Nets are discovered at runtime: any net the drill has a comp for, plus any net with a
        // connector adjacent to the drill. Any net whose storages/converters accept the item is used.
        public static int TryPipe(ThingWithComps drill, ThingDef def, int count)
        {
            Init();
            if (compResourceType == null || pipeNetProp == null || count <= 0)
            {
                return 0;
            }
            try
            {
                foreach (object net in CandidateNets(drill))
                {
                    float ratio = FindRatio(net, def);
                    if (ratio <= 0f)
                    {
                        continue;
                    }
                    float available = Convert.ToSingle(GetMember(net, "AvailableCapacity"));
                    int fit = Math.Min(count, (int)Math.Floor(available / ratio));
                    if (fit <= 0)
                    {
                        continue;
                    }
                    MethodInfo? method = net.GetType().GetMethod("DistributeAmongStorage", new[] { typeof(float), typeof(float).MakeByRefType() });
                    if (method == null)
                    {
                        continue;
                    }
                    object[] args = { fit * ratio, 0f };
                    method.Invoke(net, args);
                    int piped = (int)Math.Floor((float)args[1] / ratio);
                    if (piped > 0)
                    {
                        return Math.Min(piped, count);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ResourceDrill] Piping failed: {ex.Message}");
            }
            return 0;
        }

        private static IEnumerable<object> CandidateNets(ThingWithComps drill)
        {
            var seen = new HashSet<object>();
            foreach (ThingComp comp in drill.AllComps)
            {
                if (compResourceType!.IsInstanceOfType(comp) && pipeNetProp!.GetValue(comp) is object own && seen.Add(own))
                {
                    yield return own;
                }
            }
            Map? map = drill.Map;
            if (map == null || managerType == null)
            {
                yield break;
            }
            MapComponent? manager = null;
            foreach (MapComponent mc in map.components)
            {
                if (managerType.IsInstanceOfType(mc))
                {
                    manager = mc;
                    break;
                }
            }
            if (!(GetMember(manager, "pipeNets") is IEnumerable nets))
            {
                yield break;
            }
            var adjacent = new HashSet<IntVec3>(GenAdj.CellsAdjacent8Way(drill));
            foreach (object net in nets)
            {
                if (seen.Contains(net) || !(GetMember(net, "connectors") is IEnumerable connectors))
                {
                    continue;
                }
                foreach (object connector in connectors)
                {
                    if (connector is ThingComp tc && tc.parent != drill && tc.parent.Spawned && adjacent.Contains(tc.parent.Position))
                    {
                        seen.Add(net);
                        yield return net;
                        break;
                    }
                }
            }
        }

        private static float FindRatio(object net, ThingDef def)
        {
            if (GetMember(net, "storages") is IEnumerable storages)
            {
                foreach (object storage in storages)
                {
                    object? props = GetMember(storage, "props");
                    if (props == null)
                    {
                        continue;
                    }
                    foreach (string optionName in new[] { "extractOptions", "refillOptions" })
                    {
                        float r = RatioFromOption(GetMember(props, optionName), def);
                        if (r > 0f)
                        {
                            return r;
                        }
                    }
                }
            }
            if (GetMember(net, "resourceConverters") is IEnumerable converters)
            {
                foreach (object converter in converters)
                {
                    float r = RatioFromOption(GetMember(converter, "props"), def);
                    if (r > 0f)
                    {
                        return r;
                    }
                }
            }
            return 0f;
        }

        private static float RatioFromOption(object? option, ThingDef def)
        {
            if (option == null || GetMember(option, "thing") as ThingDef != def)
            {
                return 0f;
            }
            object? ratio = GetMember(option, "ratio");
            return ratio == null ? 0f : Convert.ToSingle(ratio);
        }

        private static object? GetMember(object? obj, string name)
        {
            if (obj == null)
            {
                return null;
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (Type? t = obj.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo? field = t.GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(obj);
                }
                PropertyInfo? prop = t.GetProperty(name, flags);
                if (prop != null)
                {
                    return prop.GetValue(obj);
                }
            }
            return null;
        }
    }
}
