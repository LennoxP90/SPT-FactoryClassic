using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace FactoryClassic.Client
{
    // BepInEx reads the cfg once at start; this watches the file and reloads it so an edit between
    // raids applies without a relaunch. Every value is read through ConfigEntry.Value at the point of
    // use, never cached, which is what makes the reload effective: one cached value and that feature
    // quietly stops reloading.
    internal sealed class ConfigReload : MonoBehaviour
    {
        static ConfigFile _config;
        static FileSystemWatcher _watcher;
        static volatile bool _dirty;

        // Its own GameObject: with HideManagerGameObject on, the plugin's own object gets no reliable Update.
        internal static void Install(ConfigFile config)
        {
            var host = new GameObject("FC_ConfigReload") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _config = config;
            var path = config.ConfigFilePath;
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, __) => _dirty = true;
            host.AddComponent<ConfigReload>();
        }

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                _config.Reload();
                Plugin.Log.LogInfo($"[FC] config reloaded, prompt={Plugin.PromptSelection.Value} default={Plugin.DefaultSelection.Value}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[FC] config reload failed: {e.Message}");
            }
        }
    }
}
