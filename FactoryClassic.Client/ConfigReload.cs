using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Watches the cfg file and reloads it, so an edit between raids applies without a relaunch.
    /// Never cache a ConfigEntry.Value anywhere: read it at the point of use, or that one setting
    /// quietly stops reloading.
    /// </summary>
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
                Plugin.Log.LogInfo($"[FC] config reloaded, routing={Plugin.SpatialRouting.Value} portals={Plugin.WirePortals.Value} "
                                 + $"lootClusters={Plugin.RepairLootClusters.Value} cameraReport={Plugin.CameraReport.Value}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[FC] config reload failed: {e.Message}");
            }
        }
    }
}
