using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Ambient players build their Fader and EmptyClip in Awake and serialize neither, so a player whose GameObject
    /// is never activated has both null. The Halloween sound scene has such players, and the first day-time blend
    /// stops them: FadeOut throws, AmbientAudioSystem.Init aborts, and the raid has no ambience. Each gets only what
    /// Awake would have built, at the moment it is needed, so stopping it is a silent fade. A bank with no clips gets
    /// the empty clip the game falls back to instead of a throw in OnPlay. A blender whose mixer parameters did not
    /// survive the rip (the game's AudioMixer assets cannot ride in a bundle) skips that write instead of throwing,
    /// so the ambience plays without the indoor/outdoor mixer blend. And an EventSoundBankChanger whose per-event
    /// bank came through the rip as null would hand that null to every player under it at raid start, silencing
    /// them all; a null swap is refused and the player keeps the bank the scene authored.
    /// The event's own layer (its banks and loops) waits for the game to announce the Halloween event, which 4.1
    /// never does here, so the Halloween sound scene announces it to its own changers when it loads.
    /// </summary>
    internal static class AmbientPlayerRepair
    {
        static PropertyInfo _fader, _emptyClip;
        static Type _faderType;

        internal static void Install()
        {
            var player = AccessTools.TypeByName("Audio.AmbientSubsystem.BaseAmbientSoundPlayer");
            _faderType = AccessTools.TypeByName("Audio.AuxiliaryAudioUtils.AudioFader");
            _fader = player == null ? null : AccessTools.Property(player, "Fader");
            _emptyClip = player == null ? null : AccessTools.Property(player, "EmptyClip");
            var fadeOut = player == null ? null : AccessTools.Method(player, "FadeOut");
            var getClip = AccessTools.Method(AccessTools.TypeByName("Audio.AmbientSubsystem.BaseRandomAmbientSoundPlayer"), "GetClip");
            if (_faderType == null || _fader == null || _emptyClip == null || fadeOut == null || getClip == null)
            {
                Plugin.Log.LogWarning("[AmbientRepair] ambient player members not found in this build; leaving them alone");
                return;
            }

            var harmony = new Harmony(BuildInfo.Guid + ".ambientrepair");
            harmony.Patch(fadeOut, prefix: new HarmonyMethod(typeof(AmbientPlayerRepair), nameof(BeforeFadeOut)));
            harmony.Patch(getClip, postfix: new HarmonyMethod(typeof(AmbientPlayerRepair), nameof(AfterGetClip)));

            var changeBank = AccessTools.Method(AccessTools.TypeByName("Audio.AmbientSubsystem.BaseRandomAmbientSoundPlayer"), "ChangeSoundBank");
            if (changeBank == null) Plugin.Log.LogWarning("[AmbientRepair] ChangeSoundBank not found; a null event bank will still silence its players");
            else harmony.Patch(changeBank, prefix: new HarmonyMethod(typeof(AmbientPlayerRepair), nameof(BeforeChangeBank)));

            // The blender's one write to the mixer, taking (parameter type, parameters, fader, value, duration).
            var blend = AccessTools.GetDeclaredMethods(AccessTools.TypeByName("Audio.AmbientSubsystem.AmbientSoundBlender"))
                .Find(m => m.Name == "method_1" && m.GetParameters().Length == 5);
            if (blend == null) Plugin.Log.LogWarning("[AmbientRepair] AmbientSoundBlender's mixer write not found; a blender without mixer data will still throw");
            else harmony.Patch(blend, prefix: new HarmonyMethod(typeof(AmbientPlayerRepair), nameof(BeforeMixerWrite)));
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, _) =>
            {
                if (scene.name != FactoryClassic.Shared.FactoryScenes.HalloweenSoundScene) return;
                try
                {
                    var changers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<Audio.AmbientSubsystem.IEventSoundContentChanger>(
                        System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(), r => r.GetComponentsInChildren<MonoBehaviour>(true))));
                    foreach (var c in changers) c.ChangeSoundContent(EFT.EEventType.Halloween);
                    Plugin.Log.LogInfo($"[AmbientRepair] told {changers.Count} event sound changer(s) it is Halloween");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[AmbientRepair] Halloween event sound failed, the event layer stays silent: {e}");
                }
            };
            Plugin.Log.LogInfo("[AmbientRepair] armed");
        }

        static void BeforeFadeOut(object __instance)
        {
            try
            {
                if (IsNull(_fader.GetValue(__instance))) _fader.SetValue(__instance, Activator.CreateInstance(_faderType));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"[AmbientRepair] Fader on {__instance}: {e.GetType().Name}: {e.Message}");
            }
        }

        static int _nullBanks;

        static bool BeforeChangeBank(Component __instance, object __0)
        {
            // A null bank is a valid vanilla state elsewhere; only the Halloween scene's changers came through blank.
            if (!IsNull(__0) || __instance == null || __instance.gameObject.scene.name != FactoryClassic.Shared.FactoryScenes.HalloweenSoundScene) return true;
            if (_nullBanks++ == 0) Plugin.Log.LogWarning("[AmbientRepair] an event bank changer handed a player a null bank; keeping the authored bank");
            return false;
        }

        static bool _mixerLogged;

        static bool BeforeMixerWrite(object __1, object __2)
        {
            if (__1 != null && __2 != null) return true;
            if (!_mixerLogged)
            {
                _mixerLogged = true;
                Plugin.Log.LogWarning("[AmbientRepair] an ambient blender has no mixer parameters; skipping its mixer writes");
            }
            return false;
        }

        static void AfterGetClip(object __instance, ref AudioClip __result)
        {
            if (!IsNull(__result)) return;
            try
            {
                var empty = _emptyClip.GetValue(__instance) as AudioClip;
                if (IsNull(empty))
                {
                    var rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
                    empty = AudioClip.Create("empty_clip", 4096, 1, rate, false);
                    _emptyClip.SetValue(__instance, empty);
                }
                __result = empty;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"[AmbientRepair] GetClip on {__instance}: {e.GetType().Name}: {e.Message}");
            }
        }

        // Unity's fake null counts: a destroyed fader throws exactly as a missing one does.
        static bool IsNull(object value) => value == null || (value is UnityEngine.Object o && o == null);
    }
}
