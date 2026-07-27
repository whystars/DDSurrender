// DDSurrenderPlugin.cs
using CommandSystem;
using DDSurrender.Commands;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using RemoteAdmin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EBroadcast = Exiled.API.Features.Broadcast;

namespace DDSurrender
{
    public class DDSurrenderPlugin : Plugin<SurrenderConfig>
    {
        public override string Author => "Crystal";
        public override Version RequiredExiledVersion { get; } = new Version(9, 14, 2);
        public override Version Version { get; } = new Version(3, 0, 0);

        public static DDSurrenderPlugin Instance { get; set; } = null!;

        public HashSet<string> _blacklist = new();

        private SurrenderCommand _surrenderCommand = null!;
        private Exiled.Events.Features.CustomEventHandler _onWaitingForPlayers = null!;

        private static readonly ConcurrentDictionary<string, CustomFaction> _factionStates = new();

        internal static class CustomInfoSlots
        {
            public const int SurrenderSlot = 100;
            public const int OtherSlot = 50;
        }

        private readonly ConcurrentDictionary<string, Dictionary<int, string>> _infoFragments =
            new ConcurrentDictionary<string, Dictionary<int, string>>();

        public override void OnEnabled()
        {
            Instance = this;

            Log.Info($"插件版本: {Version} 作者: {Author} 插件加载成功！！！！！");
            Log.Info($"当前配置状态：IsEnabled={Config.IsEnabled}, Debug={Config.Debug}");

            _onWaitingForPlayers = () =>
            {
                RegisterCommandSafe();
                Log.Debug($"[DEBUG] 命令注册完成于服务器准备阶段");
            };
            Exiled.Events.Handlers.Server.WaitingForPlayers.Subscribe(_onWaitingForPlayers);

            Exiled.Events.Handlers.Player.Escaping += OnPlayerEscape;
            Exiled.Events.Handlers.Player.Spawned += OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurt;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.Joined += OnPlayerJoined;
            Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
            Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;

            _factionStates.Clear();
        }

        public override void OnDisabled()
        {
            Instance = null!;

            Exiled.Events.Handlers.Server.WaitingForPlayers.Unsubscribe(_onWaitingForPlayers);
            Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Escaping -= OnPlayerEscape;
            Exiled.Events.Handlers.Player.Hurting -= OnPlayerHurt;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
            Exiled.Events.Handlers.Player.Joined -= OnPlayerJoined;
            Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;

            if (_surrenderCommand != null)
            {
                CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(_surrenderCommand);
                _surrenderCommand = null!;
            }
        }

        private void OnPlayerLeft(LeftEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId)) return;
            _factionStates.TryRemove(ev.Player.UserId, out _);
            _blacklist.Remove(ev.Player.UserId);
            _infoFragments.TryRemove(ev.Player.UserId, out _);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerEscape(EscapingEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId)) return;

            if (IsSurrendered(ev.Player) && IsDD(ev.Player))
            {
                ev.IsAllowed = true;
                ev.NewRole = RoleTypeId.NtfSpecialist;
                return;
            }

            _factionStates.TryRemove(ev.Player.UserId, out _);
            _blacklist.Remove(ev.Player.UserId);
            _infoFragments.TryRemove(ev.Player.UserId, out _);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerChangingRole(ChangingRoleEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId)) return;
            _factionStates.TryRemove(ev.Player.UserId, out _);
            _blacklist.Remove(ev.Player.UserId);
            _infoFragments.TryRemove(ev.Player.UserId, out _);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerJoined(JoinedEventArgs ev) { }

        private void OnPlayerDied(DiedEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId)) return;
            _factionStates.TryRemove(ev.Player.UserId, out _);
            _blacklist.Remove(ev.Player.UserId);
            _infoFragments.TryRemove(ev.Player.UserId, out _);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId)) return;

            if (ev.Player.Role.Type == RoleTypeId.ClassD)
            {
                ev.Player.Broadcast(new EBroadcast()
                {
                    Duration = 8,
                    Type = Broadcast.BroadcastFlags.Normal,
                    Content = Config.BroadcastForDD,
                    Show = true
                });
            }

            _factionStates.TryRemove(ev.Player.UserId, out _);
            _blacklist.Remove(ev.Player.UserId);
            _infoFragments.TryRemove(ev.Player.UserId, out _);
            RefreshInfo(ev.Player, 0);
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            _blacklist.Clear();
            _factionStates.Clear();
            _infoFragments.Clear();

            foreach (var p in Player.List)
                RefreshInfo(p, 0);
        }

        public int TrySurrender(Player player)
        {
            if (IsSurrendered(player)) return 2;

            bool ok = _factionStates.AddOrUpdate(player.UserId,
                key => new CustomFaction { CurrentFaction = CustomFactionType.DD_Surrendered },
                (key, old) => old
            ).CurrentFaction == CustomFactionType.DD_Surrendered;

            return ok ? 1 : 0;
        }

        private bool IsDD(Player p) => p != null && p.Role?.Type == RoleTypeId.ClassD;

        private short CanAttack(Player attacker, Player target)
        {
            if (attacker == null || target == null || !target.IsHuman || !attacker.IsHuman)
                return -1;

            bool atkSurrender = IsSurrendered(attacker);
            bool tgtSurrender = IsSurrendered(target);

            if ((IsDD(attacker) && !atkSurrender) &&
                (target.Role.Team == Team.FoundationForces || target.Role.Team == Team.Scientists))
            {
                _blacklist.Add(attacker.UserId);
                RefreshInfo(attacker, 2);
                return -1;
            }

            if ((atkSurrender && target.Role.Team == Team.ChaosInsurgency) ||
                (tgtSurrender && attacker.Role.Team == Team.ChaosInsurgency))
                return 1;

            if ((atkSurrender &&
                 (target.Role.Team == Team.FoundationForces || target.Role.Team == Team.Scientists)) ||
                (tgtSurrender &&
                 (attacker.Role.Team == Team.FoundationForces || attacker.Role.Team == Team.Scientists)))
                return 0;

            return -1;
        }

        private void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (ev?.Player == null || ev?.Attacker == null) return;
            if (string.IsNullOrEmpty(ev.Attacker?.UserId) || string.IsNullOrEmpty(ev.Player?.UserId)) return;
            if (!IsDD(ev.Player) && !IsDD(ev.Attacker)) return;
            if (ev.Attacker == ev.Player) return;
            if (Server.FriendlyFire) return;

            var canAttack = CanAttack(ev.Attacker, ev.Player);
            if (canAttack == 0)
            {
                ev.IsAllowed = false;
                ev.Amount = 0;
            }
            else if (canAttack == 1)
            {
                ev.IsAllowed = true;
                ev.DamageHandler.IsFriendlyFire = true;
                ev.DamageHandler.ForceFullFriendlyFire = true;
            }
        }

        public bool IsSurrendered(Player p) =>
            p != null && !string.IsNullOrEmpty(p.UserId) &&
            _factionStates.TryGetValue(p.UserId, out var f) &&
            f.CurrentFaction == CustomFactionType.DD_Surrendered;

        private void RegisterCommandSafe()
        {
            const string commandKey = "tx";
            try
            {
                if (CommandProcessor.RemoteAdminCommandHandler.TryGetCommand(commandKey, out var existing))
                {
                    Log.Debug($"检测到旧命令实例：{existing.GetType().Name}");
                    CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(existing);
                }

                _surrenderCommand = new SurrenderCommand();
                CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(_surrenderCommand);
                Log.Debug($"命令注册完成：{_surrenderCommand.Command}");
            }
            catch (Exception ex)
            {
                Log.Error($"注册失败：{ex}");
            }
        }

        public void RefreshInfo(Player ply, int force = -1)
        {
            if (ply == null || string.IsNullOrEmpty(ply.UserId)) return;

            var dict = _infoFragments.GetOrAdd(ply.UserId, _ => new Dictionary<int, string>());

            if (force != -1)
            {
                switch (force)
                {
                    case 1:
                        ply.InfoArea |= PlayerInfoArea.CustomInfo;
                        dict[CustomInfoSlots.SurrenderSlot] = Config.SuDD;
                        break;
                    case 2:
                        ply.InfoArea |= PlayerInfoArea.CustomInfo;
                        dict[CustomInfoSlots.SurrenderSlot] = Config.ZFDD;
                        break;
                    case 0:
                        dict.Remove(CustomInfoSlots.SurrenderSlot);
                        break;
                }
            }
            else
            {
                if (IsSurrendered(ply) && IsDD(ply))
                {
                    ply.InfoArea |= PlayerInfoArea.CustomInfo;
                    dict[CustomInfoSlots.SurrenderSlot] = Config.SuDD;
                }
                else if (_blacklist.Contains(ply.UserId) && IsDD(ply))
                {
                    ply.InfoArea |= PlayerInfoArea.CustomInfo;
                    dict[CustomInfoSlots.SurrenderSlot] = Config.ZFDD;
                }
                else
                    dict.Remove(CustomInfoSlots.SurrenderSlot);
            }

            var ordered = dict.OrderByDescending(kv => kv.Key).Select(kv => kv.Value);
            var final = string.Join(" ", ordered);

            if (final == string.Empty)
                ply.InfoArea &= ~PlayerInfoArea.CustomInfo;

            ply.CustomInfo = string.IsNullOrWhiteSpace(final) ? null : final;
        }

        /// <summary>
        /// 允许其他插件安全地向玩家的CustomInfo添加/修改标签片段。
        /// </summary>
        /// <param name="ply">要修改的玩家。</param>
        /// <param name="slotId">标签的槽位ID。建议使用一个独特的ID来避免冲突。</param>
        /// <param name="fragment">要添加的标签字符串。传入null或string.Empty将移除此槽位的标签。</param>
        public void SetPlayerInfoFragment(Player ply, int slotId, string fragment)
        {
            if (ply == null || string.IsNullOrEmpty(ply.UserId)) return;

            ply.InfoArea |= PlayerInfoArea.CustomInfo;

            var dict = _infoFragments.GetOrAdd(ply.UserId, _ => new Dictionary<int, string>());

            if (string.IsNullOrEmpty(fragment))
                dict.Remove(slotId);
            else
                dict[slotId] = fragment;

            RefreshInfo(ply);
        }
    }
}
