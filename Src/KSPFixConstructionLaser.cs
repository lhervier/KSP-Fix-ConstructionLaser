using System;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.fix.constructionlaser
{
    /// <summary>
    /// Installs the fix the settings turn on, once, when KSP starts: from then on, in a game that is not in English,
    /// an engineer is recognised as one, and welds the parts attached in EVA construction mode.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPFixConstructionLaser : MonoBehaviour
    {
        private const string HarmonyId = "com.github.lhervier.ksp.fix.constructionlaser";

        private void Start()
        {
            Settings.Load();
            Log.Info($"Version {typeof(KSPFixConstructionLaser).Assembly.GetName().Version}, " +
                $"traitsComparedByName {Settings.TraitsComparedByName}, log level {Log.Level}");
            if (!Settings.TraitsComparedByName)
            {
                return;
            }
            try
            {
                int patched = TraitsComparedByName.Install(new Harmony(HarmonyId));
                Log.Info($"Traits compared by name fix installed in {patched} stock method(s)");
            }
            catch (Exception e)
            {
                Log.Error("Could not install the traits compared by name fix, engineers do not weld in a translated " +
                    $"game, as on stock: {e}");
            }
        }
    }
}
