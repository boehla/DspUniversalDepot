// DSP game stubs — minimal type definitions so the plugin compiles
// without a live DSP install. These are SHADOWED at runtime by the
// real types from Assembly-CSharp.dll.
//
// In real DSP builds, the compiler picks up the real types via
// HAS_DSP_REFS = true (set when DSP_GAME_PATH is provided or when
// refs are present).
//
// These stubs exist only for sandbox/CI builds where we don't have
// access to a real DSP installation.

#if !HAS_DSP_REFS

// Minimal DSP types. Real types in DSP live in the global namespace
// (no namespace declaration in Assembly-CSharp.dll), so we mirror that.
public class StorageComponent
{
    public int entityId;
    public int GetItemCount(int itemId) => 0;
    public int TakeItem(int filterFrom, int filterTo, int itemId, int count) => 0;
    public int AddItem(int itemId, int count) => count;
}

public class PlanetFactory
{
    public EntityData[] entityPool = System.Array.Empty<EntityData>();
    public void RemoveEntityData(int id) { }
}

public struct EntityData
{
    public int id;
    public int protoId;
}

public class GameSave
{
    public void SaveCurrentGame() { }
    public void LoadCurrentGame() { }
}

public static class GameMain
{
    public static GameData gameData;
    public static PlanetData localPlanet;
}

public class GameData
{
    public GalaxyData galaxy;
}

public class GalaxyData
{
    public PlanetData[] planets;
}

public class PlanetData
{
    public PlanetFactory factory;
}

public class UIChatWindow
{
    public void OnSubmitText(ref string text) { }
}

#endif
