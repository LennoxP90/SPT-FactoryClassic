using System;
using System.Linq;
using System.Reflection;

namespace FactoryClassic.Client.Api
{
    // Logs the public surface at load, exactly as a consumer reading it by reflection would see it.
    //
    // The point is that the API is consumed WITHOUT a compile-time reference, so nothing breaks at
    // build time if a member is renamed or its type changes - it breaks silently in someone else's
    // mod, weeks later. Printing what reflection actually finds turns that into a line in our own log
    // that a bug report can be checked against.
    internal static class ApiSelfCheck
    {
        internal static void Run()
        {
            try
            {
                var type = typeof(Factory);

                var constants = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral)
                    .Select(field => $"{field.Name}={field.GetRawConstantValue()}");

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Select(property => $"{property.Name}:{property.PropertyType.Name}");

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsSpecialName)
                    .Select(method => $"{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name))})");

                var events = type.GetEvents(BindingFlags.Public | BindingFlags.Static).Select(e => e.Name);

                Plugin.Log.LogInfo($"[Api] {type.FullName} v{Factory.ApiVersion}");
                Plugin.Log.LogInfo($"[Api]   const: {string.Join(", ", constants)}");
                Plugin.Log.LogInfo($"[Api]   props: {string.Join(", ", properties)}");
                Plugin.Log.LogInfo($"[Api]   methods: {string.Join(", ", methods)}");
                Plugin.Log.LogInfo($"[Api]   events: {string.Join(", ", events)}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Api] self check failed: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
