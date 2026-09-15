using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Audio.SpatialSystem;
using EFT.DataProviding;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    // Repairs for the legacy Factory_Sound scene. BSG ships these scenes but never loads them, so
    // nothing caught their unassigned references:
    //
    //   1. OcclusionSettings is unassigned, and method_0 dereferences it.
    //   2. 9 of 99 audio portals have an empty _connectedRooms, so CreatePortalData throws.
    //   3. factory_sound.audiobakedata is in the 2025-12 encoding and desyncs at pair 1 of 1550;
    //      the client's own empty table is obsolete too. Both are replaced from plugin-data.
    //   4. SpatialAudioLocationInfo reports no routes and no portals, so the propagation job gets
    //      empty buffers and slices past the end of them.
    //
    // An empty table is a downgrade, not a neutral fallback: 4.1 routes every occluder except Fast
    // through the baked table. 0.14.9 needed none because it traversed roomConnections at runtime.
    internal static class SpatialAudioRepair
    {
        const string EmptyBake = "plugin-data/factory_classic_empty.audiobakedata";

        // The shipped classic table, re-encoded for the reader 4.1 actually has. See
        // analysis/convert_audiobake.py: the file BSG ships is real routing data - 1550 room pairs -
        // in the layout of 2025-12, and was never rebaked when the per-pair trailer changed.
        const string ClassicBake = "plugin-data/factory_classic.audiobakedata";

        static FieldInfo _connectedRooms;
        static SpatialAudioRoom[] _rooms;

        // One place decides which table is in play, so BakeRedirect and LocationInfoCapacities cannot
        // disagree about which file the scheduler is being sized for.
        internal static string ChosenBake()
        {
            var routing = Path.Combine(Plugin.PluginDir, ClassicBake);
            if (Plugin.SpatialRouting.Value && File.Exists(routing)) return routing;

            var empty = Path.Combine(Plugin.PluginDir, EmptyBake);
            return File.Exists(empty) ? empty : null;
        }

        // Seek: the table is 890 KB and only its last eight bytes are wanted.
        static byte[] TailOf(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < AudioBakeCapacities.TailLength) return null;
                stream.Seek(-AudioBakeCapacities.TailLength, SeekOrigin.End);

                var tail = new byte[AudioBakeCapacities.TailLength];
                var read = stream.Read(tail, 0, tail.Length);
                return read == tail.Length ? tail : null;
            }
        }

        internal static void Install()
        {
            var harmony = new Harmony(BuildInfo.Guid + ".spatialaudio");
            harmony.PatchAll(typeof(OcclusionSettingsFill));
            harmony.PatchAll(typeof(LocationInfoCapacities));
            harmony.PatchAll(typeof(PortalDataGuard));
            harmony.PatchAll(typeof(DoorOcclusionGuard));
            harmony.PatchAll(typeof(BakeRedirect));
            Plugin.Log.LogInfo("[SpatialAudio] armed");
        }

        // A portal CreatePortalData can read without throwing: it dereferences portalCollider,
        // FrontRoom and BackRoom, and the latter two are _connectedRooms[0] and [1].
        internal static bool IsWired(BaseSpatialAudioPortal portal)
        {
            if (portal == null || portal.portalCollider == null) return false;
            _connectedRooms = _connectedRooms ?? AccessTools.Field(typeof(BaseSpatialAudioPortal), "_connectedRooms");
            if (_connectedRooms == null) return false;
            if (!(_connectedRooms.GetValue(portal) is IList rooms) || rooms.Count < 2) return false;
            return rooms[0] is UnityEngine.Object front && front != null
                && rooms[1] is UnityEngine.Object back && back != null;
        }

        // Rooms are looked up from the loaded scenes rather than from SpatialAudioSystem's own
        // storage: the storage is private and is still being filled while portal data is built, so
        // asking it here would answer "no room" for everything.
        // The game tests containment against the SERIALIZED _bounds (ISpatialAudioRoom.Bounds), not
        // against GetRoomBounds(), which recomputes from the room's AudioTriggerAreas and returns a
        // zero-extent box at the transform when a room has none. Prefer the serialized value and
        // fall back to the computed one, so a room populated either way is usable.
        static Bounds BoundsOf(SpatialAudioRoom room)
        {
            var bounds = room.Bounds;
            if (HasVolume(bounds)) return bounds;
            return room.GetRoomBounds();
        }

        static bool HasVolume(Bounds bounds)
        {
            var size = bounds.size;
            return size.x > 0.01f && size.y > 0.01f && size.z > 0.01f;
        }

        static void Census()
        {
            _rooms = Resources.FindObjectsOfTypeAll<SpatialAudioRoom>();
            var usable = 0;
            foreach (var room in _rooms)
                if (room != null && HasVolume(BoundsOf(room))) usable++;

            Plugin.Log.LogInfo($"[SpatialAudio] {_rooms.Length} room(s) loaded, {usable} with a usable volume");
            if (usable == 0 && _rooms.Length > 0)
            {
                var sample = _rooms[0];
                Plugin.Log.LogWarning($"[SpatialAudio] no room has bounds; e.g. '{sample.name}' id={sample.ID} "
                                      + $"serialised={sample.Bounds} computed={sample.GetRoomBounds()} "
                                      + $"areas={(sample.Areas == null ? -1 : sample.Areas.Count)}");
            }
        }

        // The scene's own answer to what a portal connects. SpatialAudioRoom.Initialize fills
        // _connectedRooms from the ROOM side, so a portal gets two rooms only when both rooms list it;
        // one entry is enough here, because a RoomConnection names the room at the far end.
        static Dictionary<short, KeyValuePair<SpatialAudioRoom, SpatialAudioRoom>> _declared;

        static void IndexDeclaredConnections()
        {
            _declared = new Dictionary<short, KeyValuePair<SpatialAudioRoom, SpatialAudioRoom>>();
            if (_rooms == null) Census();

            foreach (var room in _rooms)
            {
                if (room == null || room.roomConnections == null) continue;
                foreach (var connection in room.roomConnections)
                {
                    if (connection == null || connection.connectedRoom == null || connection.connectingPortals == null)
                        continue;
                    foreach (var portal in connection.connectingPortals)
                    {
                        if (portal == null || _declared.ContainsKey(portal.ID)) continue;
                        _declared[portal.ID] = new KeyValuePair<SpatialAudioRoom, SpatialAudioRoom>(room, connection.connectedRoom);
                    }
                }
            }
            Plugin.Log.LogInfo($"[SpatialAudio] {_declared.Count} portal(s) have a declared room pair in the scene's roomConnections");
        }

        static bool TryWireFromDeclaration(BaseSpatialAudioPortal portal)
        {
            if (_declared == null) IndexDeclaredConnections();
            if (!_declared.TryGetValue(portal.ID, out var pair)) return false;
            if (pair.Key == null || pair.Value == null || pair.Key == pair.Value) return false;

            portal._connectedRooms.Clear();
            portal._connectedRooms.Add(pair.Key);
            portal._connectedRooms.Add(pair.Value);
            Plugin.Log.LogInfo($"[SpatialAudio] wired portal {portal.ID} '{portal.name}' to rooms {pair.Key.ID} and {pair.Value.ID} "
                               + "from the scene's own roomConnections");
            return true;
        }

        // Is the point inside this box, tested in the box's own space so rotation is handled? The
        // smallest containing box's volume comes back with it.
        static bool Inside(BoxCollider box, Vector3 point, out float volume)
        {
            volume = 0f;
            if (box == null) return false;

            var local = box.transform.InverseTransformPoint(point) - box.center;
            var half = box.size * 0.5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
                return false;

            var size = Vector3.Scale(box.size, box.transform.lossyScale);
            volume = size.x * size.y * size.z;
            return true;
        }

        // A room is the union of its AudioTriggerAreas, NOT their bounding box. Testing the box is
        // what made every portal report the same room on each side: adjacent rooms' boxes overlap
        // right through the opening, so both sample points landed in one of them.
        static SpatialAudioRoom RoomAt(Vector3 point)
        {
            if (_rooms == null) Census();

            SpatialAudioRoom best = null;
            var bestVolume = float.MaxValue;
            foreach (var room in _rooms)
            {
                if (room == null || room.Areas == null) continue;
                foreach (var area in room.Areas)
                {
                    if (area == null) continue;
                    if (!Inside(area.GetCollider(), point, out var volume)) continue;

                    // Rooms nest - a cupboard inside a hall holds the same point as the hall - so
                    // the tightest area wins.
                    if (volume < bestVolume) { best = room; bestVolume = volume; }
                }
            }
            return best;
        }

        // The rooms either side of a portal, inferred from its own collider.
        //
        // A portal is a thin slab standing in the opening it represents, so stepping out along its
        // thinnest axis lands in one room on each side. That is the only geometry involved, and it
        // is the portal's own, so a portal BSG placed correctly but never connected is recovered
        // without guessing where anything belongs.
        internal static bool TryWire(BaseSpatialAudioPortal portal)
        {
            var box = portal.portalCollider;
            if (box == null) return false;

            var t = box.transform;
            var size = Vector3.Scale(box.size, t.lossyScale);

            Vector3 axis;
            float thickness;
            if (size.x <= size.y && size.x <= size.z) { axis = t.right; thickness = size.x; }
            else if (size.y <= size.z)                { axis = t.up;    thickness = size.y; }
            else                                      { axis = t.forward; thickness = size.z; }

            var centre = t.TransformPoint(box.center);
            var first = Mathf.Max(0.5f, (thickness * 0.5f) + 0.35f);

            // A doorway can be deeper than one step: a gate set into a thick wall puts both samples
            // inside the wall's own room. Walk outwards until the two sides disagree, rather than
            // giving up on the first distance that happens not to work.
            SpatialAudioRoom front = null, back = null;
            var step = first;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                step = first + (attempt * 0.75f);
                front = RoomAt(centre + (axis * step));
                back = RoomAt(centre - (axis * step));
                if (front != null && back != null && front != back) break;
            }

            // Still the same room means the step never left it: the portal is not in an opening
            // between two rooms, and inventing a connection would be worse than none.
            if (front == null || back == null || front == back)
            {
                Plugin.Log.LogWarning($"[SpatialAudio] portal {portal.ID} '{portal.name}' at {centre}: stepping out to {step:0.00} m along "
                                      + $"{axis} found front={(front == null ? "none" : front.ID.ToString())} "
                                      + $"back={(back == null ? "none" : back.ID.ToString())}");
                return false;
            }

            portal._connectedRooms.Clear();
            portal._connectedRooms.Add(front);
            portal._connectedRooms.Add(back);
            Plugin.Log.LogInfo($"[SpatialAudio] wired portal {portal.ID} '{portal.name}' at {centre} to rooms {front.ID} and {back.ID}");
            return true;
        }

        // What CreatePortalData would have produced, for a portal whose rooms it cannot read.
        //
        // The rooms are used for FOUR SCALARS and nothing else - two wall-occlusion values and two
        // outdoor flags. Everything that matters geometrically comes from the portal itself, so a
        // portal with no rooms still has a perfectly good normal, size and centre.
        //
        // That is why default(AudioPortalData) was the wrong stand-in: it zeroes portalNormal and
        // portalHalfSize, and a zero normal normalises to NaN. With no routing table nothing ever
        // read those fields, so it looked harmless for as long as the table stayed empty.
        static RoomPair.AudioPortalData Describe(BaseSpatialAudioPortal portal)
        {
            // Both sides are the same room when a portal sits inside one, which is exactly the case
            // for the portals that cannot be wired - so one lookup answers for both.
            var room = portal.portalCollider != null ? RoomAt(portal.portalCollider.bounds.center) : null;
            var occlusion = room != null ? room.WallOcclusion : 0.5f;
            // The mask directly, rather than the IsOutdoor extension, which is not reachable from here.
            var outdoor = room != null && (room.Type & EAudioRoomTypeMask.Outdoor) != 0;

            return new RoomPair.AudioPortalData
            {
                portalNormal = portal.transform.forward,
                portalRight = portal.transform.right,
                portalUp = portal.transform.up,
                portalHalfSize = portal.GetPortalHalfSize(),
                closureLevel = portal.PortalClosureLevel,
                traversalMaxCost = portal.traversalMaxCost,
                depth = portal.Depth,
                center = portal.portalCollider != null ? portal.portalCollider.bounds.center : portal.transform.position,
                frontRoomWallOcclusion = occlusion,
                backRoomWallOcclusion = occlusion,
                isFrontRoomOutdoor = outdoor,
                isBackRoomOutdoor = outdoor,
            };
        }

        [HarmonyPatch]
        internal static class OcclusionSettingsFill
        {
            static MethodBase TargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Initialize));

            static void Prefix(SpatialAudioSystem __instance)
            {
                try
                {
                    if (__instance == null || !PresetSwap.ClassicLoaded()) return;

                    var field = AccessTools.Field(typeof(SpatialAudioSystem), "OcclusionSettings");
                    if (field == null) { Plugin.Log.LogError("[SpatialAudio] OcclusionSettings field not found"); return; }
                    if (field.GetValue(__instance) is UnityEngine.Object present && present != null) return;

                    // Apply() fills it from the backend settings, so a blank instance is enough to get
                    // past the dereference with the values the game would have used anyway.
                    // The scene changes between raids, so a cached room list from the last one would
                    // point at destroyed objects.
                    _rooms = null;
                    _declared = null;

                    var blank = ScriptableObject.CreateInstance(field.FieldType);
                    blank.name = "FactoryClassic_OcclusionSettings";
                    field.SetValue(__instance, blank);
                    Plugin.Log.LogWarning("[SpatialAudio] OcclusionSettings was unassigned on the classic sound scene; supplied one");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[SpatialAudio] OcclusionSettings fill failed: {e}");
                }
            }
        }

        // Sizes the propagation job buffers for the table being installed. The classic tile's asset
        // reports zero routes and zero portals, and the first warm-up then slices a zero-length
        // NativeArray. The asset is shared, so the edit outlives the raid; Raise makes it idempotent.
        [HarmonyPatch]
        internal static class LocationInfoCapacities
        {
            static MethodBase TargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Initialize));

            static void Prefix(SpatialAudioSystem __instance)
            {
                try
                {
                    if (__instance == null || !PresetSwap.ClassicLoaded()) return;

                    // Initialize logs its own error and bails when this is missing; nothing to size.
                    var info = __instance.LocationInfo;
                    if (info == null) return;

                    var bake = ChosenBake();
                    if (bake == null) return;
                    if (!AudioBakeCapacities.TryReadTail(TailOf(bake), out var routes, out var portals))
                    {
                        Plugin.Log.LogError($"[SpatialAudio] could not read the capacities from '{Path.GetFileName(bake)}'; "
                                            + "leaving the location info alone rather than guessing");
                        return;
                    }

                    var raisedRoutes = AudioBakeCapacities.Raise(info.maxRoutesCount, routes);
                    var raisedPortals = AudioBakeCapacities.Raise(info.maxRoutePortalsCount, portals);
                    if (raisedRoutes == info.maxRoutesCount && raisedPortals == info.maxRoutePortalsCount) return;

                    Plugin.Log.LogWarning($"[SpatialAudio] the classic tile's location info carried "
                                          + $"maxRoutesCount={info.maxRoutesCount} maxRoutePortalsCount={info.maxRoutePortalsCount}; "
                                          + $"raised to {raisedRoutes}/{raisedPortals} from {Path.GetFileName(bake)}");
                    info.maxRoutesCount = raisedRoutes;
                    info.maxRoutePortalsCount = raisedPortals;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[SpatialAudio] location info capacities failed: {e}");
                }
            }
        }

        // The guard belongs on the method that dereferences, not on one caller. AddStaticPortalData is
        // only the load-time path; UpdateInitialPortalsData is the second, called from
        // GameWorld.OnGameStarted when the spawn countdown ends. A throw there aborts OnGameStarted
        // itself, so every system the world starts afterwards silently never runs - which presented as
        // the map loading, rendering, and the process dying seconds later.
        [HarmonyPatch]
        internal static class PortalDataGuard
        {
            static int _skipped;

            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(SpatialAudioDataContainer), nameof(SpatialAudioDataContainer.CreatePortalData));

            static bool Prefix(BaseSpatialAudioPortal portal, ref RoomPair.AudioPortalData __result)
            {
                if (IsWired(portal)) return true;

                // Recover it rather than skip it. A skipped portal is not merely silent: with a real
                // routing table its all-zero data sits on live paths, which is what killed the raid
                // when SpatialRouting was first turned on.
                if (portal != null && Plugin.WirePortals.Value)
                {
                    try
                    {
                        // Declared first, sampled only where there is no declaration.
                        if (TryWireFromDeclaration(portal) && IsWired(portal)) return true;
                        if (TryWire(portal) && IsWired(portal)) return true;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"[SpatialAudio] could not wire '{portal.name}': {e.GetType().Name}: {e.Message}");
                    }
                }

                try
                {
                    __result = Describe(portal);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[SpatialAudio] could not describe '{portal.name}': {e.GetType().Name}: {e.Message}");
                    __result = default(RoomPair.AudioPortalData);
                }
                _skipped++;
                if (_skipped <= 12)
                    Plugin.Log.LogWarning($"[SpatialAudio] portal {(portal == null ? -1 : portal.ID)} "
                                          + $"'{(portal == null ? "<null>" : portal.name)}' connects no rooms and sits inside one, "
                                          + "so it is described from its own geometry rather than connected");
                return false;
            }
        }

        // CheckOcclusion reads portal.FrontRoom.ID with no null check, and the call sits inside
        // CG_SmoothDoorOpenCoroutine - an NRE there kills the coroutine and the door stops part way
        // open. False is what the method returns anyway when it has no usable portal.
        [HarmonyPatch]
        internal static class DoorOcclusionGuard
        {
            static int _reported;

            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(InteractiveObjectOccluder), nameof(InteractiveObjectOccluder.CheckOcclusion));

            static bool Prefix(string targetID, ref bool __result)
            {
                try
                {
                    if (!PresetSwap.ClassicLoaded()) return true;
                    if (string.IsNullOrEmpty(targetID)) return true;
                    if (!DataProvider.TryGetData<SpatialAudioDataContainer>(out var container)) return true;
                    if (!container.TryGetInteractivePortalById(targetID, out var portal) || portal == null) return true;
                    if (portal.FrontRoom != null && portal.BackRoom != null) return true;

                    _reported++;
                    if (_reported <= 8)
                        Plugin.Log.LogWarning($"[SpatialAudio] interactive portal '{targetID}' ({portal.name}) connects no rooms, "
                                              + "so its occlusion is skipped rather than throwing inside the door coroutine");
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[SpatialAudio] door occlusion guard failed: {e}");
                    return true;
                }
            }
        }

        [HarmonyPatch]
        internal static class BakeRedirect
        {
            // Path.Combine returns a rooted second argument unchanged, and the constructor does
            // Path.Combine(streamingAssetsPath, dataPath), so an absolute path beside the plugin is
            // taken as-is.
            static MethodBase TargetMethod() =>
                AccessTools.Constructor(typeof(SpatialAudioDataLoader), new[] { typeof(string), typeof(MonoBehaviour) });

            static void Prefix(ref string dataPath)
            {
                try
                {
                    if (!PresetSwap.ClassicLoaded()) return;

                    // Routing first, empty table second. Either is better than the shipped file,
                    // which the current reader desyncs on and which fails the raid rather than the
                    // sound.
                    var ours = ChosenBake();
                    if (ours == null)
                    {
                        Plugin.Log.LogError($"[SpatialAudio] {EmptyBake} is missing beside the dll; the malformed shipped table " +
                                            "will be read and the raid will fail to load");
                        return;
                    }

                    if (ours.EndsWith(Path.GetFileName(ClassicBake), StringComparison.Ordinal))
                        Plugin.Log.LogInfo($"[SpatialAudio] classic tile: '{dataPath}' -> our converted routing table");
                    else
                        Plugin.Log.LogInfo($"[SpatialAudio] classic tile: '{dataPath}' -> our empty routing table"
                                           + (Plugin.SpatialRouting.Value ? " (the converted table is missing)" : " (SpatialRouting is off)"));
                    dataPath = ours;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[SpatialAudio] bake redirect failed: {e}");
                }
            }
        }
    }
}
