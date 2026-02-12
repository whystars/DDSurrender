// SurrenderCommand.cs
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;
using RemoteAdmin;
using System;
using System.Collections.Concurrent;

namespace DDSurrender.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))] // 服务器控制台
    // 修复点1：移除IWrapCommand接口
    public class SurrenderCommand : ICommand
    {
        // 移除确认相关字段
        public string Command => "tx";
        public string[] Aliases => new[] { "ddsurrender", "surrender" };

        public string Description => DDSurrenderPlugin.Instance.Config.command_description;
        // private static readonly ConcurrentDictionary<string, DateTime> _pendingConfirms = new();
        private static readonly object _instanceLock = new object();
        private static readonly ConcurrentDictionary<string, DateTime> _pendingConfirms = new();
        private static SurrenderCommand _instance;

        public static SurrenderCommand Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new SurrenderCommand();
                    }
                }
                return _instance;
            }
        }

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "";
            var player = Player.Get(sender);

            //旧玩家信息
            if (player?.Role.Type != RoleTypeId.ClassD) // 检查玩家身份是否为DD
            {
                response = DDSurrenderPlugin.Instance.Config.NotClassD;
                return false;
            }

            if(DDSurrenderPlugin.Instance._blacklist.Contains(player.UserId)) // 检查玩家是否在DD黑名单中
            { 
                response = DDSurrenderPlugin.Instance.Config.Zaofan;
                //添加ZFDD到玩家名称
                DDSurrenderPlugin.Instance.RefreshInfo(player, 2);
                return false;
            }

            // 直接处理投降
            int state = DDSurrenderPlugin.Instance.TrySurrender(player);
            bool ok = false;
            switch (state)
            {
                case 2:
                    response = DDSurrenderPlugin.Instance.Config.SuAgain;
                    ok = false;
                    break;
                case 1:
                    response = DDSurrenderPlugin.Instance.Config.SuSuccess;

                    //添加SuDD到玩家名称
                    DDSurrenderPlugin.Instance.RefreshInfo(player, 1);

                    ok = true;
                    break;
                case 0:
                    response = DDSurrenderPlugin.Instance.Config.FailedCausedUnknown;
                    ok = false;
                    break;
                default:
                    break;
            }
            return ok;
        }

        public static class CommandRegistry
        {
            private static readonly ConcurrentDictionary<string, ICommand> _commands = new();

            public static void Register(string key, Func<ICommand> factory)
            {
                if (_commands.ContainsKey(key))
                {
                    _commands[key] = factory();
                    return;
                }
                _commands.TryAdd(key, factory());
            }

            public static void UnregisterAll()
            {
                foreach (var cmd in _commands.Values)
                {
                    CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(cmd);
                }
                _commands.Clear();
            }
        }
    }
}