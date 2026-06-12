// _Stubs.cs — minimal stub types so the plugin COMPILES without the
// proprietary BepInEx.Core.dll (which is not distributed on NuGet or
// GitHub releases).
//
// In a real DSP runtime, BepInEx's preloader loads Assembly-CSharp.dll
// + BepInEx.Core.dll FIRST; the plugin DLL is loaded after and references
// the REAL types via the resolver. These stubs exist only so the C#
// compiler can produce the .dll in our Linux/CI build.
//
// To verify the real runtime types, build on Windows with:
//   set DSP_GAME_PATH=...
//   dotnet build -c Release
// or download a real BepInEx.Core.dll from your DSP install and add
// it to ./libs/.

#if !HAS_DSP_REFS

namespace BepInEx
{
    public class BasePlugin
    {
        public BepInEx.Logging.ManualLogSource Log { get; } =
            new BepInEx.Logging.ManualLogSource("StubBasePlugin");
        public BepInEx.Configuration.ConfigFile Config { get; } =
            new BepInEx.Configuration.ConfigFile();
        public virtual void Load() { }
        public virtual bool Unload() => true;
    }

    public class ManualLogSource
    {
        public void LogInfo(object msg) { }
        public void LogWarning(object msg) { }
        public void LogError(object msg) { }
        public void LogMessage(object msg) { }
        public void LogDebug(object msg) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class BepInPluginAttribute : System.Attribute
    {
        public string GUID;
        public string Name;
        public string Version;
        public BepInPluginAttribute(string guid, string name, string version)
        {
            GUID = guid; Name = name; Version = version;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class BepInProcessAttribute : System.Attribute
    {
        public string ProcessName;
        public BepInProcessAttribute(string processName) { ProcessName = processName; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class BepInDependencyAttribute : System.Attribute
    {
        public string DependencyGUID;
        public BepInDependencyAttribute(string guid) { DependencyGUID = guid; }
    }

    public static class Plugin
    {
        public class LogSource { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
            => new ConfigEntry<T>();
    }

    public class ConfigEntry<T>
    {
        public T Value { get; set; }
    }
}

namespace BepInEx.Logging
{
    // already provided by real BepInEx.dll
}

namespace BepInEx.Unity.IL2CPP
{
    public class IL2CPPBasePlugin : BepInEx.BasePlugin { }
}

// MiniJSON stub (DSP has it built-in, but we don't have DSP DLLs)
public static class MiniJSON
{
    public static string Serialize(object obj) => "{}";
    public static object Deserialize(string json) => null;
}

// DSPModSave stub
namespace DSPModSave
{
    public static class SaveDataManager
    {
        public static void SetSaveData(string key, string data) { }
        public static string GetSaveData(string key) => "";
    }
}

// UIRoot stub (DSP UI root for audio/UI)
public class UIRoot
{
    public static UIRoot instance;
    public UIGame uiGame;
}

public class UIGame
{
    public void PlayAudioClip(int id) { }
}

#endif
