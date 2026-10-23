using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vampirizm.Core;

namespace Vampirizm.Commands
{
    internal sealed class VampireCommand
    {
        public string Name => "vampirizm";
        public string Help => "status | convert | cure | blood <amount> | level <0..100> (self only; requires devcommands)";
        public List<string> CommandOptionList() => new List<string> { "status", "convert", "cure", "blood", "level" };

        internal static void Register()
        {
            var command = new VampireCommand();
            // Valheim 1.0.17 adds hideBehindDevCommands to the constructor.
            // Jotunn 2.29.2's CommandManager only recognizes the old signature.
            // hideBehindDevCommands only hides it from help/autocomplete; Run enforces devcommands.
            // isCheat is not used: it requires being the server and marks the profile as cheated.
            new Terminal.ConsoleCommand(command.Name, command.Help,
                (Terminal.ConsoleEvent)(args => command.Run(args.Args.Skip(1).ToArray(), args.Context)),
                hideBehindDevCommands: true, optionsFetcher: command.CommandOptionList);
        }

        public void Run(string[] args, Terminal context)
        {
            if (!Terminal.m_cheat)
            {
                Reply(context, "vampirizm_devcommands_required");
                return;
            }
            if (Plugin.Instance == null || Player.m_localPlayer == null || ZNet.instance == null)
            {
                Reply(context, "vampirizm_no_player");
                return;
            }
            var controller = VampireController.Ensure(Player.m_localPlayer);
            string command = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            if (command == "status" && args.Length <= 1)
            {
                Status(context, controller);
                return;
            }
            if (!controller.CanChange)
            {
                Reply(context, "vampirizm_unavailable");
                return;
            }
            if (command == "convert" && args.Length == 1) controller.Convert();
            else if (command == "cure" && args.Length == 1) controller.Cure();
            else if ((command == "blood" || command == "level") && args.Length == 2 &&
                float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) &&
                BloodRules.IsFinite(amount) && amount >= 0f && (command != "level" || amount <= 100f))
            {
                if (!controller.State.IsVampire)
                {
                    Reply(context, "vampirizm_not_vampire");
                    return;
                }
                if (command == "blood") controller.AddBlood(amount);
                else controller.SetLevel(amount);
            }
            else
            {
                context?.AddString(Name + " " + Help);
                return;
            }
            Status(context, controller);
        }

        private static void Reply(Terminal context, string key) => context?.AddString(global::Localization.instance.Localize("$" + key));

        private static void Status(Terminal context, VampireController controller)
        {
            if (!controller.State.IsVampire) { Reply(context, "vampirizm_not_vampire"); return; }
            context?.AddString(string.Format(CultureInfo.InvariantCulture,
                global::Localization.instance.Localize("$vampirizm_status"),
                controller.State.Blood, controller.Capacity, controller.Level));
        }
    }
}
