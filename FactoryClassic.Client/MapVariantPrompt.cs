using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FactoryClassic.Client
{
    // Asks which Factory to load before the map screen advances.
    //
    // It does NOT rewire the Next and Ready buttons, which is the obvious approach and the wrong one.
    // InterchangeRework does exactly that, and so did the first version of this: both mods postfix
    // MatchMakerSelectionLocationScreen.Awake and both call OnClick.RemoveAllListeners() before adding
    // their own, so whichever wires last silently deletes the other's prompt. There is no ordering
    // that leaves both working.
    //
    // Instead this patches the DESTINATION - the screen controller's ShowNextScreen and
    // ShowReadyScreen - so every mod that rewires those buttons still funnels through here, however
    // many of them there are, and nobody has to know about anybody else.
    [HarmonyPatch]
    internal static class MapVariantPrompt
    {
        static bool _controllerPatched;

        // Captured at patch time. Asking Harmony for MethodBase __originalMethod instead makes it emit
        // a GetMethodFromHandle call, which THROWS for a method whose declaring type is an open
        // generic - and ShowNextScreen is declared on EftSequenceScreenController<TController,TScreen>.
        // That did not fail to hook the button, it broke it: every click threw inside the wrapper.
        static MethodInfo _next, _ready;

        // The one controller this prompt is for. ShowNextScreen is declared on a GENERIC base, and
        // Mono shares native code across reference-type instantiations, so patching it patches every
        // screen in the matchmaker sequence - the Fika co-op screen included. Without this filter the
        // prompt reappears on each of them, and invoking the stored MethodInfo against a different
        // instantiation throws "Object does not match target type".
        static Type _controllerType;

        // Set just before the original is re-invoked with the answer, so the prefix lets that one
        // call through instead of prompting again.
        static bool _passThrough;

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".prompt").PatchAll(typeof(MapVariantPrompt));
            Plugin.Log.LogInfo("[MapChoice] armed");
        }

        // Still a postfix on Awake, but only to get hold of the controller: its type is nested and
        // cannot be resolved by name, so the instance is where it comes from. Every mod's postfix
        // runs, so this is unaffected by the button fight.
        [HarmonyPatch(typeof(MatchMakerSelectionLocationScreen), nameof(MatchMakerSelectionLocationScreen.Awake))]
        [HarmonyPostfix]
        static void PatchController(MatchMakerSelectionLocationScreen __instance)
        {
            try
            {
                if (_controllerPatched) return;

                var controller = Controller(__instance);
                if (controller == null) { Plugin.Log.LogWarning("[MapChoice] no screen controller on the map screen; no prompt"); return; }

                _controllerType = controller.GetType();

                var harmony = new Harmony(BuildInfo.Guid + ".screen");
                _next = Declared(controller.GetType(), "ShowNextScreen");
                _ready = Declared(controller.GetType(), "ShowReadyScreen");

                var patched = 0;
                if (_next != null) { harmony.Patch(_next, new HarmonyMethod(AccessTools.Method(typeof(MapVariantPrompt), nameof(BeforeNext)))); patched++; }
                if (_ready != null) { harmony.Patch(_ready, new HarmonyMethod(AccessTools.Method(typeof(MapVariantPrompt), nameof(BeforeReady)))); patched++; }

                _controllerPatched = patched > 0;
                Plugin.Log.LogDebug($"[MapChoice] hooked {patched} advance method(s); ShowNextScreen declared on " +
                                    $"'{_next?.DeclaringType?.FullName ?? "not found"}'");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[MapChoice] could not hook the screen controller; no prompt: {e}");
            }
        }

        // Walks up from the concrete controller so the method comes off the CLOSED constructed base
        // type rather than the open generic definition.
        static MethodInfo Declared(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var found = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public
                                                  | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (found != null) return found;
            }
            Plugin.Log.LogWarning($"[MapChoice] {type.Name}.{name} not found on any base type");
            return null;
        }

        // One prefix per button, so neither needs __originalMethod to know which it is.
        static bool BeforeNext(object __instance) => BeforeAdvance(__instance, _next, "ShowNextScreen");

        static bool BeforeReady(object __instance) => BeforeAdvance(__instance, _ready, "ShowReadyScreen");

        // Returning false stops the screen advancing until the player answers.
        static bool BeforeAdvance(object __instance, MethodInfo original, string name)
        {
            try
            {
                if (_passThrough) { _passThrough = false; return true; }

                // Any other screen in the sequence shares this patched method; it is not ours.
                if (__instance == null || __instance.GetType() != _controllerType) return true;

                var settings = AccessTools.Field(__instance.GetType(), "RaidSettings")?.GetValue(__instance) as RaidSettings;
                var locationId = settings?.SelectedLocation?.Id ?? "";
                var ours = FactoryScenes.ServerNames.Any(id => string.Equals(id, locationId, StringComparison.OrdinalIgnoreCase));

                Plugin.Log.LogDebug($"[MapChoice] {name}: location '{(locationId.Length == 0 ? "none" : locationId)}', ours={ours}, ask={Plugin.PromptSelection.Value}");
                if (!ours) return true;

                Action proceed = () =>
                {
                    _passThrough = true;
                    try { original?.Invoke(__instance, null); }
                    catch (Exception e) { _passThrough = false; Plugin.Log.LogError($"[MapChoice] advancing the screen failed: {e}"); }
                };

                // No prompt does not mean no work: the server serves whichever variant it last
                // recorded, and a fresh session has recorded nothing, so skipping the post would
                // silently give everyone the shipped map however DefaultSelection is set.
                if (!Plugin.PromptSelection.Value)
                {
                    // No window to warn in, so the log is the only place left to say it. The raid is
                    // not blocked: the player turned the prompt off, which is their call to make.
                    if (MapVariant.IsClassic(Plugin.DefaultVariant))
                    {
                        var silent = QuestGateSync.AcceptedBlocked();
                        if (silent.Length > 0)
                            Plugin.Log.LogWarning("[QuestGate] the prompt is off, so these accepted quests load into a "
                                                  + "classic raid they cannot be finished in: " + string.Join(", ", silent));
                    }

                    Choose(Plugin.DefaultVariant, locationId, proceed, "config");
                    return false;
                }

                var preview = FindPreview();
                MapChoiceWindow.Show(
                    TileImage("factory_vanilla.png") ?? preview,
                    TileImage("factory_classic.png") ?? preview,
                    QuestGateSync.AcceptedBlocked(),
                    choice =>
                    {
                        if (choice == null) { Plugin.Log.LogDebug("[MapChoice] cancelled; staying on the map screen"); return; }
                        Choose(choice.Value ? MapVariant.Classic : MapVariant.Original, locationId, proceed, "player");
                    });

                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[MapChoice] prefix failed, the screen advances unprompted: {e}");
                return true;
            }
        }

        static void Choose(string variant, string locationId, Action proceed, string source)
        {
            PresetSwap.SessionChoice = variant;
            VariantSync.Post(locationId, variant);
            Plugin.Log.LogInfo($"[MapChoice] {source} chose {VariantDisplay.For(variant)}");
            proceed();
        }

        static object Controller(MatchMakerSelectionLocationScreen screen)
            => AccessTools.Field(screen.GetType(), "ScreenController")?.GetValue(screen)
            ?? AccessTools.Property(screen.GetType(), "ScreenController")?.GetValue(screen);

        // One screenshot per variant from plugin-data/ui; a missing file falls back to the game's own
        // preview, so shipping without images degrades to something sensible rather than a blank tile.
        static readonly Dictionary<string, Sprite> Tiles = new Dictionary<string, Sprite>();

        static Sprite TileImage(string file)
        {
            if (Tiles.TryGetValue(file, out var cached)) return cached;

            Sprite sprite = null;
            var path = System.IO.Path.Combine(Plugin.PluginDir, "plugin-data", "ui", file);
            if (System.IO.File.Exists(path))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(System.IO.File.ReadAllBytes(path)))
                {
                    texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    sprite.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                    Plugin.Log.LogDebug($"[MapChoice] tile image {file} {texture.width}x{texture.height}");
                }
                else Plugin.Log.LogWarning($"[MapChoice] {file} did not decode as an image");
            }

            Tiles[file] = sprite;
            return sprite;
        }

        // The largest Factory-named sprite currently loaded. Searched globally rather than through the
        // screen, because the prefix runs from the controller and has no screen reference.
        static bool _previewReported;

        static Sprite FindPreview()
        {
            var best = Resources.FindObjectsOfTypeAll<Sprite>()
                .Where(s => s != null && (s.name ?? "").IndexOf("factory", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(s => s.rect.width * s.rect.height)
                .FirstOrDefault();

            if (!_previewReported)
            {
                _previewReported = true;
                Plugin.Log.LogDebug(best == null
                    ? "[MapChoice] no Factory preview sprite found; the tiles show a flat colour"
                    : $"[MapChoice] preview sprite '{best.name}' {best.rect.width}x{best.rect.height}");
            }

            return best;
        }
    }
}
