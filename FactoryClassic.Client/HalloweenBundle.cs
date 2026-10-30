using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The Halloween scenes, shipped beside the dll. Loaded on the first Halloween raid and kept: the
    /// scenes have to be loadable by path when the preset loads.
    /// </summary>
    internal static class HalloweenBundle
    {
        static AssetBundle _bundle;
        static bool _tried;
        static string[] _scenePaths = new string[0];

        internal static string[] ScenePaths => _scenePaths;

        static string Dir => Path.Combine(Plugin.PluginDir, "plugin-data", "halloween");

        // False, logged once, whenever the bundle is missing or does not match its stamp: the raid
        // then loads plain classic.
        internal static bool Ensure()
        {
            if (_bundle) return true;
            if (_tried) return false;
            _tried = true;

            var bundlePath = Path.Combine(Dir, "factoryclassic_halloween.bundle");
            var stampPath = Path.Combine(Dir, "halloween-stamp.json");
            try
            {
                if (!File.Exists(bundlePath) || !File.Exists(stampPath))
                {
                    Plugin.Log.LogError($"[Halloween] bundle or stamp missing in {Dir}; Halloween raids load plain classic");
                    return false;
                }

                var stamp = JObject.Parse(File.ReadAllText(stampPath));
                var bytes = (long?)stamp["BundleBytes"];
                if (bytes != new FileInfo(bundlePath).Length)
                {
                    Plugin.Log.LogError($"[Halloween] bundle is {new FileInfo(bundlePath).Length} bytes, its stamp says {bytes}; Halloween disabled");
                    return false;
                }

                _bundle = AssetBundle.LoadFromFile(bundlePath);
                if (!_bundle)
                {
                    Plugin.Log.LogError($"[Halloween] AssetBundle.LoadFromFile failed for {bundlePath}; Halloween disabled");
                    return false;
                }

                _scenePaths = _bundle.GetAllScenePaths();
                Plugin.Log.LogInfo($"[Halloween] bundle loaded: {string.Join(", ", _scenePaths.Select(Path.GetFileNameWithoutExtension).ToArray())}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Halloween] loading the bundle failed, Halloween disabled: {e}");
                return false;
            }
        }
    }
}
