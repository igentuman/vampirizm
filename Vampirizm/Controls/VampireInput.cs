using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Controls
{
    internal enum VampireAction { Dash, Invisibility, BatForm, BloodRage }

    internal sealed class VampireBinding
    {
        internal VampireAction Action { get; }
        internal ButtonConfig Button { get; }
        internal ConfigEntry<KeyCode> Key { get; }
        internal string LabelToken { get; }

        internal VampireBinding(VampireAction action, ButtonConfig button, ConfigEntry<KeyCode> key, string labelToken)
        {
            Action = action;
            Button = button;
            Key = key;
            LabelToken = labelToken;
        }
    }

    // Independent actions registered through Jotunn. The local BepInEx config is the source of truth:
    // Jotunn rebuilds the bindings from it on every ZInput.Load, so rebinds from the Controls menu are mirrored back.
    internal static class VampireInput
    {
        private static readonly List<VampireBinding> All = new List<VampireBinding>();
        private static readonly Func<Player, bool> TakeInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));
        private static readonly Func<KeyCode, bool, string> KeyCodeToPath =
            AccessTools.MethodDelegate<Func<KeyCode, bool, string>>(AccessTools.Method(typeof(ZInput), "KeyCodeToPath",
                new[] { typeof(KeyCode), typeof(bool) }));
        private static Dictionary<string, KeyCode> _keysByPath;

        internal static IReadOnlyList<VampireBinding> Bindings => All;

        internal static void Register(ConfigFile config)
        {
            Add(config, VampireAction.Dash, "Dash", KeyCode.LeftAlt);
            Add(config, VampireAction.Invisibility, "Invisibility", KeyCode.H);
            Add(config, VampireAction.BatForm, "Bat form", KeyCode.B);
            Add(config, VampireAction.BloodRage, "Blood rage", KeyCode.Q);
        }

        private static void Add(ConfigFile config, VampireAction action, string title, KeyCode key)
        {
            string id = action.ToString().ToLowerInvariant();
            var keyEntry = config.Bind("Controls", title + " key", key,
                "Keyboard or mouse button. Rebinding it in Valheim's Controls menu is saved here.");
            // Valheim cancels interactive rebinding on any gamepad input, so gamepad buttons are set only here.
            var gamepadEntry = config.Bind("Controls", title + " gamepad button", InputManager.GamepadButton.None,
                "Gamepad button; None disables it. Every gamepad button already has a vanilla action, so pick one you do not use.");
            // These settings are local. Save immediately so keyboard rebinds and gamepad edits
            // survive a crash or a relaunch without depending on a later config shutdown save.
            keyEntry.SettingChanged += (sender, args) => Plugin.SaveControlSettings();
            gamepadEntry.SettingChanged += (sender, args) => Plugin.SaveControlSettings();
            var button = new ButtonConfig
            {
                Name = "Vampirizm" + action,
                Config = keyEntry,
                GamepadConfig = gamepadEntry,
                HintToken = "$vampirizm_action_" + id
            };
            // Jotunn appends the mod GUID to Name; use button.Name from here on.
            InputManager.Instance.AddButton(Plugin.PluginGuid, button);
            All.Add(new VampireBinding(action, button, keyEntry, "$vampirizm_control_" + id));
        }

        // Gameplay input only: no chat, console, inventory, map, menus or an active rebind.
        internal static bool Pressed(Player player, VampireAction action)
        {
            if (ZInput.s_IsRebindActive || player == null || !TakeInput(player)) return false;
            foreach (VampireBinding binding in All)
                if (binding.Action == action) return ZInput.GetButtonDown(binding.Button.Name);
            return false;
        }

        // Called after ZInput.Save. Must never break Valheim's own settings save.
        internal static void MirrorRebinds()
        {
            if (ZInput.instance == null) return;
            bool changed = false;
            foreach (VampireBinding binding in All)
            {
                try
                {
                    var definition = ZInput.instance.GetButtonDef(binding.Button.Name);
                    if (definition == null) continue;
                    string path = definition.GetActionPath() ?? "";
                    KeyCode key = KeyCode.None;
                    if (path.Length > 0 && !KeysByPath().TryGetValue(path, out key))
                    {
                        Plugin.Warn($"Cannot save {binding.Action} binding with unsupported input path: {path}");
                        continue;
                    }
                    if (binding.Key.Value != key)
                    {
                        binding.Key.Value = key;
                        changed = true;
                    }
                }
                catch (Exception exception)
                {
                    Plugin.Warn($"Cannot save {binding.Action} binding: {exception}");
                }
            }

            // SettingChanged normally saves this too; explicitly flush after the complete
            // rebind so all bindings are written together after Valheim's own save.
            if (changed) Plugin.SaveControlSettings();
        }

        private static Dictionary<string, KeyCode> KeysByPath()
        {
            if (_keysByPath != null) return _keysByPath;
            var map = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase);
            // Unmapped key codes fall back to the path of KeyCode.None.
            string unmapped = KeyCodeToPath(KeyCode.None, false);
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key >= KeyCode.JoystickButton0) continue;
                string path = KeyCodeToPath(key, false);
                // Several KeyCode aliases share a path; keep the first, canonical one.
                if (!string.IsNullOrEmpty(path) && path != unmapped && !map.ContainsKey(path)) map[path] = key;
            }
            return _keysByPath = map;
        }
    }
}
