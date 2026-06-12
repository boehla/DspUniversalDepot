// Auto-generated stubs for offline build (DSP not available).
// These types are ONLY compiled when DSP_GAME_PATH is NOT set.
// On Windows with real DSP DLLs, this file is excluded from compilation.
#if !HAS_DSP_REFS
#pragma warning disable CS0067, CS0169, CS0649, CS0414

using System;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInPluginAttribute : Attribute
    {
        public string GUID;
        public string Name;
        public string Version;
        public BepInPluginAttribute(string guid, string name, string version)
        {
            GUID = guid; Name = name; Version = version;
        }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class BepInProcessAttribute : Attribute
    {
        public BepInProcessAttribute(string processName) { }
    }

    public abstract class BasePlugin
    {
        public Configuration.ConfigFile Config { get; set; } = new Configuration.ConfigFile();
        public virtual void Load() { }
    }
}

namespace BepInEx.Unity.IL2CPP
{
    public abstract class BasePlugin : BepInEx.BasePlugin
    {
        protected BepInEx.Logging.ManualLogSource Log { get; set; }
    }

    // Alias used in the plugin to disambiguate
    public abstract class IL2CPPBasePlugin : BasePlugin { }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public ManualLogSource(string name) { }
        public void LogInfo(object msg) { }
        public void LogMessage(object msg) { }
        public void LogWarning(object msg) { }
        public void LogError(object msg) { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
            => new ConfigEntry<T> { Value = defaultValue };
    }
    public class ConfigEntry<T>
    {
        public T Value;
    }
}

namespace HarmonyLib
{
    public class Harmony
    {
        public Harmony(string id) { }
        public void PatchAll(System.Reflection.Assembly a) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public Type[] Type { get; set; }
        public string[] Name { get; set; }

        public HarmonyPatch() { }
        public HarmonyPatch(Type type) { Type = new[] { type }; }
        public HarmonyPatch(Type type, string methodName) { Type = new[] { type }; Name = new[] { methodName }; }
        public HarmonyPatch(Type[] types) { Type = types; }
        public HarmonyPatch(Type[] types, string[] methodNames) { Type = types; Name = methodNames; }
        public HarmonyPatch(Type type, string methodName, Type[] argTypes)
        {
            Type = new[] { type };
            Name = new[] { methodName };
            // argTypes would be used by Harmony at runtime; we just store for compile
        }
    }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyTranspiler : Attribute { }
}

namespace UnityEngine
{
    public class Object { }
    public class ScriptableObject : Object { }
    public class GameObject : Object
    {
        public string name;
        public T GetComponent<T>() => default;
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() => default;
    }
    public class Texture : Object
    {
        public int width;
        public int height;
    }
    public class Texture2D : Texture
    {
        public Texture2D() { }
        public Texture2D(int w, int h) { width = w; height = h; }
    }
    public class Sprite
    {
        public Sprite(Texture2D t, Rect r, Vector2 p) { }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p) => new Sprite(t, r, p);
    }
    public struct Rect
    {
        public Rect(float x, float y, float w, float h) { }
    }
    public struct Vector2
    {
        public Vector2(float x, float y) { }
    }
    public class AssetBundle : Object
    {
        public static AssetBundle LoadFromFile(string path) => null;
        public void Unload(bool unloadAllLoadedObjects) { }
        public T LoadAsset<T>(string name) where T : Object => null;
    }
}

// DSP-specific stub types
namespace DspUniversalDepot
{
    // Forward declaration of VFPreload (event pattern matches real DSP)
    public class VFPreload
    {
        public static event Action InvokeOnLoadWorkEnded;
    }

    // Forward declaration of StorageComponent
    public class StorageComponent
    {
        public int entityId;
        public int GetItemCount(int itemId) => 0;
        public int TakeItem(int filterFrom, int filterTo, int desiredItemId, int desiredCount) => 0;
        public int AddItem(int itemId, int count) => 0;
    }
}

#endif
