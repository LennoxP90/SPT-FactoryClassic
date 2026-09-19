using FactoryClassic.Shared;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Clears what is ours when a Factory map unloads. ManagedGuard is deliberately not cleared:
    /// what the server manages cannot change between raids.
    /// </summary>
    internal static class RaidLifecycle
    {
        internal static void Install()
        {
            SceneManager.sceneUnloaded += scene =>
            {
                if (!FactoryScenes.IsFactoryScene(scene.name)) return;
                CameraInventory.Forget();
            };
            Plugin.Log.LogInfo("[Lifecycle] armed");
        }
    }
}
