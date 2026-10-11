using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using System;
using System.Linq;
using System.Reflection;
using Vampirizm.Combat;
using Vampirizm.Commands;
using Vampirizm.Configuration;
using Vampirizm.Controls;
using Vampirizm.Feeding;
using Vampirizm.Items;
using Vampirizm.Localization;
using Vampirizm.Skills;

namespace Vampirizm
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.29.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.igentuman.vampirizm";
        public const string PluginName = "Vampirizm";
        public const string PluginVersion = "0.1.0";
        internal static Plugin Instance { get; private set; }
        internal VampireConfig Settings { get; private set; }
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Settings = new VampireConfig(Config);
            BloodItemRegistry.Initialize(Settings.BloodItems);
            SilverRegistry.Initialize(Settings.SilverItems);
            DonorRegistry.Initialize(Settings.Donors, Settings.NeverDonors, Settings.NoHoldDonors);
            VampireLocalization.Register();
            VampirismSkill.Register();
            VampireRune.Register();
            VampireCommand.Register();
            VampireInput.Register(Config);
            _harmony = new Harmony(PluginGuid);
            // Patch classes independently: a stale game method signature must not prevent
            // unrelated systems (food, HUD, persistence) from being installed.
            foreach (Type patchType in typeof(Plugin).Assembly.GetTypes()
                .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0))
            {
                try
                {
                    _harmony.CreateClassProcessor(patchType).Patch();
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Failed to install Harmony patches from {patchType.FullName}: {exception}");
                }
            }
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded; Valheim {(global::Version.GetVersionString())}");
        }

        internal static void Warn(string message) => Instance?.Logger.LogWarning(message);
        internal static void LogInfo(string message) => Instance?.Logger.LogInfo(message);

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            foreach (var controller in UnityEngine.Object.FindObjectsByType<Core.VampireController>(UnityEngine.FindObjectsSortMode.None))
                controller.Invisibility?.End();
            Instance = null;
        }
    }
}
