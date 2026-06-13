using System;
using System.Reflection;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace DspUniversalDepot {
    /// <summary>
    /// Nebula multiplayer bridge. All hard references to the Nebula API live in this file so the mod
    /// keeps working in singleplayer / without Nebula installed.
    ///
    /// IMPORTANT load-safety rule: every call that touches a Nebula type is guarded by the
    /// Nebula-free <see cref="Enabled"/> flag (set true only after a successful <see cref="TryInit"/>,
    /// which the plugin calls only when the Nebula API plugin is present). C# short-circuit evaluation
    /// then guarantees the Nebula-referencing method bodies are never JIT-compiled when Nebula is
    /// absent. The packet/processor/IMultiplayerMod types below reference Nebula types in their
    /// definitions; they are only ever loaded by Nebula's own reflection scan (which runs only when
    /// Nebula is installed). Harmony's PatchAll tolerates the unloadable types via
    /// AccessTools.GetTypesFromAssembly.
    ///
    /// What is synced: the per-building "Discard overflow" toggle (a discrete, host-authoritative UI
    /// action). Belt auto-registration of supply slots is left to Nebula's host-authoritative,
    /// deterministic factory tick — see <c>StationBeltInputPatch</c>, which skips client-side mutation
    /// in multiplayer so the host stays the single source of truth and Nebula syncs storage down.
    /// </summary>
    public static class NebulaCompat {
        private static volatile bool _active;

        /// <summary>Nebula-free gate. True only once the Nebula API is confirmed present and packets are registered.</summary>
        public static bool Enabled => _active;

        /// <summary>Call only when the Nebula API plugin is loaded (guard with a Chainloader check first).</summary>
        public static void TryInit(Assembly asm) {
            try {
                NebulaModAPI.RegisterPackets(asm);
                if(!NebulaModAPI.TargetAssemblies.Contains(asm)) NebulaModAPI.TargetAssemblies.Add(asm);
                _active = true;
                UniversalDepotPlugin.Log.LogInfo("[Depot] Nebula API detected — multiplayer sync registered.");
            } catch(Exception ex) {
                UniversalDepotPlugin.Log.LogWarning("[Depot] Nebula registration failed: " + ex.Message);
            }
        }

        /// <summary>True only while we are a connected Nebula client inside an active session.</summary>
        public static bool IsClientInMultiplayer {
            get {
                try {
                    IMultiplayerSession session = NebulaModAPI.MultiplayerSession;
                    return NebulaModAPI.IsMultiplayerActive && session != null && session.IsClient;
                } catch {
                    return false;
                }
            }
        }

        /// <summary>
        /// Propagate an overflow-toggle change. Host broadcasts to everyone on the planet; a client
        /// sends it to the host, which applies it authoritatively and re-broadcasts. Call only from the
        /// main thread (UI click) and only when <see cref="Enabled"/>.
        /// </summary>
        public static void SendOverflow(int planetId, int stationId, bool overflow) {
            try {
                if(!NebulaModAPI.IsMultiplayerActive) return;
                IMultiplayerSession session = NebulaModAPI.MultiplayerSession;
                if(session == null || session.Network == null) return;
                DepotOverflowPacket packet = new DepotOverflowPacket(planetId, stationId, overflow);
                if(session.IsServer) session.Network.SendPacketToPlanet(packet, planetId);
                else session.Network.SendPacket(packet);
            } catch(Exception ex) {
                UniversalDepotPlugin.Log.LogWarning("[Depot] SendOverflow failed: " + ex.Message);
            }
        }

        /// <summary>Set the overflow flag on the target depot station, with the usual depot guards.</summary>
        public static void ApplyOverflow(int planetId, int stationId, bool overflow) {
            try {
                PlanetData planet = GameMain.galaxy != null ? GameMain.galaxy.PlanetById(planetId) : null;
                PlanetFactory factory = planet != null ? planet.factory : null;
                PlanetTransport transport = factory != null ? factory.transport : null;
                if(transport == null || stationId <= 0 || stationId >= transport.stationCursor) return;
                StationComponent sc = transport.stationPool[stationId];
                if(sc == null || sc.id != stationId || sc.storage == null) return;
                if(sc.storage.Length <= 6 || sc.isCollector || sc.isStellar) return;   // only our depot
                sc.includeOrbitCollector = overflow;
            } catch(Exception ex) {
                UniversalDepotPlugin.Log.LogWarning("[Depot] ApplyOverflow failed: " + ex.Message);
            }
        }

        /// <summary>Re-broadcast a received overflow packet to all clients on its planet (host only).</summary>
        public static void RebroadcastOverflowToPlanet(DepotOverflowPacket packet) {
            try {
                IMultiplayerSession session = NebulaModAPI.MultiplayerSession;
                if(session == null || session.Network == null) return;
                session.Network.SendPacketToPlanet(packet, packet.PlanetId);
            } catch(Exception ex) {
                UniversalDepotPlugin.Log.LogWarning("[Depot] Rebroadcast failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Sync packet for the per-building overflow toggle. Plain data (public properties + parameterless
    /// ctor) so Nebula's serializer can handle it.
    /// </summary>
    public class DepotOverflowPacket {
        public int PlanetId { get; set; }
        public int StationId { get; set; }
        public bool Overflow { get; set; }
        public DepotOverflowPacket() { }
        public DepotOverflowPacket(int planetId, int stationId, bool overflow) {
            this.PlanetId = planetId;
            this.StationId = stationId;
            this.Overflow = overflow;
        }
    }

    /// <summary>Applies an overflow change wherever the packet lands; the host also re-broadcasts it.</summary>
    [RegisterPacketProcessor]
    public class DepotOverflowProcessor : BasePacketProcessor<DepotOverflowPacket> {
        public override void ProcessPacket(DepotOverflowPacket packet, INebulaConnection conn) {
            NebulaCompat.ApplyOverflow(packet.PlanetId, packet.StationId, packet.Overflow);
            if(IsHost) NebulaCompat.RebroadcastOverflowToPlanet(packet);
        }
    }

    /// <summary>
    /// Declares the mod to Nebula so it enforces that host and every client run the same version —
    /// the depot adds a building proto and patches station logic, so a mismatch would desync. Only
    /// ever loaded by Nebula's reflection scan, so it never touches the no-Nebula path.
    /// </summary>
    public class DepotMultiplayerMod : IMultiplayerMod {
        public string Version => UniversalDepotPlugin.VERSION;
        public bool CheckVersion(string hostVersion, string clientVersion) => hostVersion == clientVersion;
    }

    /// <summary>
    /// Sends the overflow change over the network when a player clicks the toggle on a depot. Reuses the
    /// vanilla orbital-collector click (which already flipped the local flag); we just propagate the new
    /// value. No-op in singleplayer / without Nebula via the <see cref="NebulaCompat.Enabled"/> gate.
    /// </summary>
    [HarmonyPatch(typeof(UIStationWindow), "OnIncludeOrbitCollectorClick")]
    public static class DepotOverflowClickPatch {
        [HarmonyPostfix]
        public static void Postfix(UIStationWindow __instance) {
            try {
                if(!NebulaCompat.Enabled) return;
                PlanetTransport transport = __instance.transport;
                int stationId = __instance.stationId;
                if(transport == null || stationId == 0) return;
                StationComponent sc = transport.stationPool[stationId];
                if(sc == null || sc.id != stationId || sc.storage == null) return;
                if(sc.storage.Length <= 6 || sc.isCollector || sc.isStellar) return;   // only our depot
                int planetId = __instance.factory != null ? __instance.factory.planetId : 0;
                NebulaCompat.SendOverflow(planetId, stationId, sc.includeOrbitCollector);
            } catch {
                // never let a UI click throw
            }
        }
    }
}
