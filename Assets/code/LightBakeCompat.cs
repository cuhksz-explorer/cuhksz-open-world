using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Some engine builds (e.g. Tuanjie) omit <see cref="Light.lightmapBakeType"/> on the runtime API.
/// </summary>
public static class LightBakeCompat
{
    static readonly PropertyInfo BakeTypeProp =
        typeof(Light).GetProperty("lightmapBakeType", BindingFlags.Instance | BindingFlags.Public);

    public static bool SupportsLightmapBakeType => BakeTypeProp != null;

    public static LightmapBakeType? GetLightmapBakeType(Light light)
    {
        if (light == null || BakeTypeProp == null)
            return null;
        return (LightmapBakeType)BakeTypeProp.GetValue(light);
    }

    public static void SetMixedBakeTypeIfSupported(Light light)
    {
        if (light == null || BakeTypeProp == null)
            return;
        BakeTypeProp.SetValue(light, LightmapBakeType.Mixed);
    }
}
