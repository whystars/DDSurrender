// SurrenderCommand.cs
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;
using RemoteAdmin;
using System;

namespace DDSurrender.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class SurrenderCommand : ICommand
    {
        public string Command => "tx";
        public string[] Aliases => new[] { "ddsurrender", "surrender" };
        public string Description => DDSurrenderPlugin.Instance.Config.command_description;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "";
            var player = Player.Get(sender);

            if (player?.Role.Type != RoleTypeId.ClassD)
            {
                response = DDSurrenderPlugin.Instance.Config.NotClassD;
                return false;
            }

            if (DDSurrenderPlugin.Instance._blacklist.Contains(player.UserId))
            {
                response = DDSurrenderPlugin.Instance.Config.Zaofan;
                DDSurrenderPlugin.Instance.RefreshInfo(player, 2);
                return false;
            }

            int state = DDSurrenderPlugin.Instance.TrySurrender(player);
            switch (state)
            {
                case 2:
                    response = DDSurrenderPlugin.Instance.Config.SuAgain;
                    return false;
                case 1:
                    response = DDSurrenderPlugin.Instance.Config.SuSuccess;
                    DDSurrenderPlugin.Instance.RefreshInfo(player, 1);
                    return true;
                default:
                    response = DDSurrenderPlugin.Instance.Config.FailedCausedUnknown;
                    return false;
            }
        }
    }
}
