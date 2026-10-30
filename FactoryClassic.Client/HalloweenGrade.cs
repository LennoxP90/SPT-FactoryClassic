using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The event's look is its camera: a DesaturateEffect classic's camera does not carry, a hue-focus
    /// pass that keeps reds while the rest goes grey, and retuned tone and bloom. After EffectsController.Init,
    /// a Halloween raid gets the 1.3.0 camera's values. Shaders and ramp are the game's own, so nothing ships.
    /// Effects the graphics settings switch (AO, HBAO, depth of field, chromatic aberration) stay the player's.
    /// </summary>
    internal static class HalloweenGrade
    {
        // Read from the 1.3.0 event camera (sharedassets764, Cam2_..._FactoryEvent) on 2026-10-29.
        const float WeatherDesaturate = 0.23796962f;
        const float Radius = 1f, RadiusFalloff = 0.425f;
        static readonly Vector2 MinMaxRadius = new Vector2(0.325f, 1f);
        const string ShaderName = "Hidden/Desaturate Effect";
        const string RampName = "grayscale ramp";

        // Event camera values that differ from 4.1's Cam2, read 2026-10-30. PrismEffects only up to its tone
        // values: past them the 1.3.0 and 4.1 layouts differ, so exposure, fog and DoF are not copied.
        static readonly (string Type, string Field, object Value)[] Tuning =
        {
            ("PrismEffects", "bloomIntensity", 0.05f),
            ("PrismEffects", "bloomThreshold", 1.579f),
            ("PrismEffects", "dirtIntensity", 0.759f),
            ("PrismEffects", "m_baseLightingContribution", 0.0302f),
            ("PrismEffects", "m_LightSensitivityMultiplier", 10.8f),
            ("PrismEffects", "noiseIntensity", 0.09f),
            ("PrismEffects", "toneValues", new Vector3(7f, 0.3f, 0.3f)),
            ("PrismEffects", "secondaryToneValues.x", -0.043f),
            ("PrismEffects", "secondaryToneValues.y", 0.25f),
            ("BloomAndFlares", "sepBlurSpread", 4.3f),
            ("BloomAndFlares", "bloomIntensity", 1f),
            ("BloomAndFlares", "bloomThreshold", 0.46f),
            ("BloomAndFlares", "bloomBlurIterations", 3),
            ("BloomAndFlares", "lensflareIntensity", 1f),
            ("BloomAndFlares", "lensflareThreshold", 0.3f),
            ("BloomAndFlares", "flareColorA", new Color(0.4f, 0.4f, 0.8f, 1f)),
            ("CC_HueFocus", "hue", 8f),
            ("CC_HueFocus", "range", 14f),
            ("CC_HueFocus", "boost", 1f),
            ("CC_HueFocus", "amount", 0.7f),
        };

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".halloweengrade").PatchAll(typeof(EffectsInit));
            Plugin.Log.LogInfo("[Halloween] grade armed");
        }

        [HarmonyPatch]
        internal static class EffectsInit
        {
            static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("EffectsController"), "Init");

            static void Postfix(object __instance)
            {
                try
                {
                    if (!(__instance is MonoBehaviour controller) || !PresetSwap.HalloweenLoaded()) return;
                    Apply(controller.gameObject);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Halloween] grade failed, the classic look stays: {e}");
                }
            }
        }

        static void Apply(GameObject camera)
        {
            var type = AccessTools.TypeByName("DesaturateEffect");
            var shader = Shader.Find(ShaderName);
            if (type == null || !shader)
            {
                Plugin.Log.LogError($"[Halloween] DesaturateEffect={type != null}, shader '{ShaderName}'={(bool)shader}; the classic look stays");
                return;
            }

            // The shader has to be in place before Start, which checks it and disables the effect without one.
            var effect = (Behaviour)(camera.GetComponent(type) ?? camera.AddComponent(type));
            void Set(string field, object value) => AccessTools.Field(type, field).SetValue(effect, value);
            Set("shader", shader);
            Set("textureRamp", Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t => t.name == RampName));
            Set("WeatherDesaturate", WeatherDesaturate);
            Set("MinMaxRadius", MinMaxRadius);
            Set("Radius", Radius);
            Set("RadiusFalloff", RadiusFalloff);
            effect.enabled = true;
            Plugin.Log.LogInfo($"[Halloween] grey grade on {camera.name}: desaturate {WeatherDesaturate:0.###}");
            Tune(camera);
        }

        static void Tune(GameObject camera)
        {
            var set = 0;
            foreach (var (typeName, field, value) in Tuning)
            {
                var c = camera.GetComponent(typeName);
                if (c == null || !SetPath(c, field, value)) Plugin.Log.LogWarning($"[Halloween] {typeName}.{field} not found, left as is");
                else set++;
            }

            // The event runs two hue-focus passes; only the camera's own one is used, since adding an image effect at
            // runtime froze the frame (black world, HUD smearing). It also runs the second bloom and the sharpen off.
            var hue = camera.GetComponent("CC_HueFocus") as Behaviour;
            if (hue != null)
            {
                hue.enabled = true;
            }
            var blooms = camera.GetComponents(AccessTools.TypeByName("BloomAndFlares"));
            if (blooms.Length > 1) ((Behaviour)blooms[1]).enabled = false;
            if (camera.GetComponent("CC_Sharpen") is Behaviour sharpen) sharpen.enabled = false;
            Plugin.Log.LogInfo($"[Halloween] look tuned: {set}/{Tuning.Length} values");
        }

        // "a.b" walks into a struct field and writes it back, since a boxed struct is a copy.
        static bool SetPath(object target, string path, object value)
        {
            var dot = path.IndexOf('.');
            var f = AccessTools.Field(target.GetType(), dot < 0 ? path : path.Substring(0, dot));
            if (f == null) return false;
            if (dot < 0)
            {
                try
                {
                    f.SetValue(target, f.FieldType.IsPrimitive ? Convert.ChangeType(value, f.FieldType) : value);
                    return true;
                }
                catch (Exception) { return false; }
            }
            var inner = f.GetValue(target);
            if (inner == null || !SetPath(inner, path.Substring(dot + 1), value)) return false;
            f.SetValue(target, inner);
            return true;
        }
    }
}
