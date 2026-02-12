// DDSurrenderPlugin.cs
using Achievements.Handlers;
using CommandSystem;
using DDSurrender;
using DDSurrender.Commands;
using Discord;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using LabApi.Loader;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerStatsSystem;
using RemoteAdmin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using EBroadcast = Exiled.API.Features.Broadcast;

namespace DDSurrender
{
    public class DDSurrenderPlugin : Plugin<SurrenderConfig>
    {
        public override string Author => "Crystal";
        public override Version RequiredExiledVersion { get; } = new Version(9, 6, 3); // 更新版本要求
        public override Version Version { get; } = new Version(3, 0, 0);

        public static DDSurrenderPlugin Instance { get; set; } = null!;

        //public List<Player> Blacklist; // DD黑名单
        public List<string> _blacklist = new();

        public bool ffEnabled = Server.FriendlyFire; // 记录服务器是否开启友伤

        private static readonly HashSet<string> _registeredCommands = new();
        private static readonly ConcurrentDictionary<string, CustomFaction> _factionStates = new();

        internal static class CustomInfoSlots
        {
            public const int SurrenderSlot = 100;   // DDSurrender
            public const int OtherSlot = 50;    // 预留
        }

        private readonly ConcurrentDictionary<string, Dictionary<int, string>> _infoFragments =
            new ConcurrentDictionary<string, Dictionary<int, string>>();

        public override void OnEnabled()
        {
            Instance = this;

            Log.Info($"插件版本: {Version}" + $" 作者: {Author}" + $" 插件加载成功！！！！！");
            Log.Info($"当前配置状态：IsEnabled={Config.IsEnabled}, Debug={Config.Debug}");

            // 使用延迟注册模式
            Exiled.Events.Handlers.Server.WaitingForPlayers += () =>
            {
                RegisterCommandsSafe();
                Log.Debug($"[DEBUG] 命令注册完成于服务器准备阶段");
            };

            // 注册事件
            Exiled.Events.Handlers.Player.Escaping += OnPlayerEscape;
            Exiled.Events.Handlers.Player.Spawned += OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurt;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.Joined += OnPlayerJoined;
            Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
            Exiled.Events.Handlers.Player.Left += OnPlayerLeft;

            // 订阅回合结束事件
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;

            _factionStates.Clear();
        }

        public override void OnDisabled()
        {
            Instance = null!;
            // 注销事件
            Exiled.Events.Handlers.Player.Spawned -= OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Escaping -= OnPlayerEscape;
            Exiled.Events.Handlers.Player.Hurting -= OnPlayerHurt;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
            Exiled.Events.Handlers.Player.Joined -= OnPlayerJoined;
            Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

            // 注销回合结束事件
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;

            // 注销命令
            foreach (var cmdKey in _registeredCommands)
            {
                if (CommandProcessor.RemoteAdminCommandHandler.TryGetCommand(cmdKey, out ICommand cmd))
                    CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(cmd);
            }
            _registeredCommands.Clear();
        }
        
        private void OnPlayerLeft(LeftEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;

            // 清除字典状态
            _factionStates.TryRemove(ev.Player.UserId, out _);

            // 移除DD黑名单 
            _blacklist.Remove(ev.Player.UserId);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerEscape(EscapingEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;

            if (IsSurrendered(ev.Player) && IsDD(ev.Player))
            {

                ev.IsAllowed = true;

                ev.NewRole = RoleTypeId.NtfSpecialist;

                return;
            }

            // 清除玩家标签
            _factionStates.TryRemove(ev.Player.UserId, out _);

            // 移除DD黑名单
            _blacklist.Remove(ev.Player.UserId);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerChangingRole(ChangingRoleEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;

            // 清除字典状态
            _factionStates.TryRemove(ev.Player.UserId, out _);

            // 移除DD黑名单
            _blacklist.Remove(ev.Player.UserId);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerJoined(JoinedEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;
        }

        // 新增玩家状态重置
        private void OnPlayerDied(DiedEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;

            // 清除字典状态
            _factionStates.TryRemove(ev.Player.UserId, out _);

            // 移除DD黑名单 
            _blacklist.Remove(ev.Player.UserId);
            RefreshInfo(ev.Player, 0);
        }

        private void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            if (string.IsNullOrEmpty(ev.Player?.UserId))
                return;

            if (ev.Player.Role.Type == RoleTypeId.ClassD)
            {
                EBroadcast broadcast = new EBroadcast()
                {
                    Duration = 8,
                    Type = Broadcast.BroadcastFlags.Normal,
                    Content = Config.BroadcastForDD,
                    Show = true
                };
                ev.Player.Broadcast(broadcast);
            }

            // 重生时强制清除（防止残留）
            _factionStates.TryRemove(ev.Player.UserId, out _);

            // 移除DD黑名单
            _blacklist.Remove(ev.Player.UserId);
            RefreshInfo(ev.Player, 0);
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            // 1. 移除DD黑名单
            _blacklist.Clear();

            // 2. 清所有状态字典
            _factionStates.Clear();

            // 3. 清悬浮文本
            foreach (var p in Player.List)
            {
                RefreshInfo(p, 0);
            }
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

        // 修改CanAttack方法（增加状态优先级判断）
        private short CanAttack(Player attacker, Player target)
        {
            if (attacker == null || target == null || !target.IsHuman || !attacker.IsHuman)
                return -1;

            bool atkSurrender = IsSurrendered(attacker);
            bool tgtSurrender = IsSurrendered(target);

            if ((IsDD(attacker) && !atkSurrender) &&
                (target.Role.Team == Team.FoundationForces
                || target.Role.Team == Team.Scientists))  //DD攻击MTF/科学家/保安
            {
                _blacklist.Add(attacker.UserId);
                RefreshInfo(attacker, 2);
                return -1;
            }

            /* 1. 投降 DD ↔ 混沌 可互打 */
            if ((atkSurrender && target.Role.Team == Team.ChaosInsurgency) ||
                (tgtSurrender && attacker.Role.Team == Team.ChaosInsurgency))
                return 1;

            /* 2. 投降 DD 与 MTF/科学家/保安 互斥 */
            if ((atkSurrender &&
                 (target.Role.Team == Team.FoundationForces || target.Role.Team == Team.Scientists)) ||
                (tgtSurrender &&
                 (attacker.Role.Team == Team.FoundationForces || attacker.Role.Team == Team.Scientists)))
                return 0;

            return -1;
        }

        // 修改OnPlayerHurt事件处理
        private void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (ev?.Player == null || ev?.Attacker == null) return; //确保有玩家

            if (string.IsNullOrEmpty(ev.Attacker?.UserId) || string.IsNullOrEmpty(ev.Player?.UserId)) //确保有UserId
                return;

            if (!IsDD(ev.Player) && !IsDD(ev.Attacker)) return; //确保有一个是DD

            if (ev.Attacker == ev.Player) return; //自己打自己放行

            if (ffEnabled) return; //开了友伤就放行

            var damage = ev.Amount;

            var canAttack = CanAttack(ev.Attacker, ev.Player);
            if (canAttack == 0)
            {
                ev.IsAllowed = false;
                ev.Amount = 0;
                return;
            }
            else if (canAttack == 1)
            {
                ev.IsAllowed = true;
                ev.DamageHandler.IsFriendlyFire = true;
                ev.DamageHandler.ForceFullFriendlyFire = true;
                return;
            }
        }

        //判断投降状态
        public bool IsSurrendered(Player p) =>
            p != null && !string.IsNullOrEmpty(p.UserId) &&
            _factionStates.TryGetValue(p.UserId, out var f) &&
            f.CurrentFaction == CustomFactionType.DD_Surrendered;

        private void RegisterCommandsSafe()
        {
            const string commandKey = "tx";
            try
            {
                if (CommandProcessor.RemoteAdminCommandHandler.TryGetCommand(commandKey, out var cmd))
                {
                    Log.Debug($"检测到旧命令实例：{cmd.GetType().Name}");
                    CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(cmd);
                }

                var newCmd = new SurrenderCommand();
                CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(newCmd);
                Log.Debug($"命令注册完成：{newCmd.Command} (哈希:{newCmd.GetHashCode()})");
            }
            catch (Exception ex)
            {
                Log.Error($"注册失败：{ex}");
            }
        }

        public void RefreshInfo(Player ply, int force = -1)
        {
            if (ply == null || string.IsNullOrEmpty(ply.UserId))
            {
                return;
            }

            var dict = _infoFragments.GetOrAdd(ply.UserId, _ => new Dictionary<int, string>());

            // 1. 优先处理强制操作
            if (force != -1)
            {
                switch (force)
                {
                    case 1:
                        dict[CustomInfoSlots.SurrenderSlot] = Config.SuDD;
                        break;
                    case 2:
                        dict[CustomInfoSlots.SurrenderSlot] = Config.ZFDD;
                        break;
                    case 0:
                        dict.Remove(CustomInfoSlots.SurrenderSlot);
                        break;
                }
            }
            // 2. 如果没有强制参数，执行常规逻辑
            else
            {
                if (IsSurrendered(ply) && IsDD(ply))
                    dict[CustomInfoSlots.SurrenderSlot] = Config.SuDD;
                else if (_blacklist.Contains(ply.UserId) && IsDD(ply))
                    dict[CustomInfoSlots.SurrenderSlot] = Config.ZFDD;
                else
                    dict.Remove(CustomInfoSlots.SurrenderSlot);
            }

            // 3. 拼接所有片段
            var ordered = dict.OrderByDescending(kv => kv.Key).Select(kv => kv.Value);
            var final = string.Join(" ", ordered);

            // 4. 写入
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
            if (ply == null || string.IsNullOrEmpty(ply.UserId))
            {
                return;
            }

            var dict = _infoFragments.GetOrAdd(ply.UserId, _ => new Dictionary<int, string>());

            if (string.IsNullOrEmpty(fragment))
            {
                dict.Remove(slotId);
            }
            else
            {
                dict[slotId] = fragment;
            }

            // 更新玩家的CustomInfo
            RefreshInfo(ply);
        }
    }
}