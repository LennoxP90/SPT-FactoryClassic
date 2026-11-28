using System;
using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The Halloween scenes' barbed wire reaches the game with no sound bank. PlayAtPoint reads the bank on its first
    /// line and throws, and PlaySound only records its sound time after that call returns, so the 0.5 s throttle never
    /// engages and every bot or player in the wire throws on each trigger entry and damage tick. Damage and the slowdown
    /// run before the sound and are unaffected. Each Halloween wire is given the game's own bank of the same name on its
    /// first sound (classic's wire uses it, so it is loaded in every Classic raid); with none found it stays silent.
    /// </summary>
    internal static class HalloweenWireSound
    {
        const string BankName = "BarbedWire";

        static readonly HashSet<int> Handled = new HashSet<int>();
        static SoundBank _gameBank;

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".wiresound").Patch(AccessTools.Method(typeof(BarbedWire), nameof(BarbedWire.PlaySound)),
                prefix: new HarmonyMethod(typeof(HalloweenWireSound), nameof(BeforePlaySound)));
            Plugin.Log.LogInfo("[Halloween] wire sound repair armed");
        }

        static bool BeforePlaySound(BarbedWire __instance)
        {
            if (!__instance.gameObject.scene.name.StartsWith(FactoryScenes.HalloweenScene, StringComparison.Ordinal)) return true;
            if (Handled.Add(__instance.GetInstanceID()) && !Usable(__instance._soundBank))
            {
                if (!Usable(_gameBank)) _gameBank = FindGameBank();
                __instance._soundBank = _gameBank;
            }
            return __instance._soundBank != null;
        }

        static SoundBank FindGameBank()
        {
            foreach (var bank in Resources.FindObjectsOfTypeAll<SoundBank>())
                if (bank.name == BankName && Usable(bank))
                {
                    Plugin.Log.LogInfo($"[Halloween] wire has no sound bank; using the game's '{BankName}'");
                    return bank;
                }
            Plugin.Log.LogWarning($"[Halloween] wire has no sound bank and the game has no '{BankName}' loaded; the wire stays silent");
            return null;
        }

        // The same call PlayAtPoint makes: a bank that yields no clip, or throws, cannot play.
        static bool Usable(SoundBank bank)
        {
            if (bank == null) return false;
            try
            {
                AudioClip a = null, b = null;
                float proportions = 1f;
                return bank.PickClips(0f, ref a, ref b, ref proportions) > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
