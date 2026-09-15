using FactoryClassic.Shared;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    // Clears the raid's answer when the map unloads, so the next raid decides for itself.
    //
    // This matters most on a Fika headless, which serves many raids in one process and never sees a
    // map screen: without it the first raid's answer would be reused for every raid afterwards.
    internal static class RaidLifecycle
    {
        internal static void Install()
        {
            SceneManager.sceneUnloaded += scene =>
            {
                if (!FactoryScenes.IsFactoryScene(scene.name)) return;
                PresetSwap.ForgetChoice();
            };
            Plugin.Log.LogInfo("[Lifecycle] armed");
        }
    }
}
