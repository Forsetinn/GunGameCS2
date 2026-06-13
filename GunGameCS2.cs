// GunGame for CS2 (CounterStrikeSharp)
// A modern port of the classic GunGame:SM gameplay by teame06 / Liam:
// kill with your current weapon to level up through the weapon order,
// finish with a knife kill to win. Supports knife steal (KnifePro),
// suicide penalty, handicap for late joiners, turbo mode, configurable
// kills-per-level and a center HUD.

using System.Drawing;
using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace GunGameCS2;

[MinimumApiVersion(260)]
public class GunGamePlugin : BasePlugin, IPluginConfig<GunGameConfig>
{
    public override string ModuleName => "GunGame CS2";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Eidur + Claude";
    public override string ModuleDescription => "Classic GunGame mode, inspired by GunGame:SM";

    public GunGameConfig Config { get; set; } = new();

    private class PlayerData
    {
        public int Level = 1;
        public int Kills;
        public int LevelsThisRound;
    }

    private readonly Dictionary<ulong, PlayerData> _players = new();
    private readonly HashSet<ulong> _soundMuted = new();
    private readonly HashSet<int> _tintedSlots = new();
    private readonly Dictionary<ulong, int> _mapVotes = new();
    private List<string> _voteOptions = new();
    private bool _voteActive;
    private string? _lastMapCycleEntry;
    private bool _winnerDeclared;
    private string? _winnerName;
    private float _winEndsAt;
    private bool _startupRedirectDone;
    private CCSGameRules? _gameRules;
    private ConVar? _ffaConVar;

    private static readonly Dictionary<string, string> WeaponDisplayNames = new()
    {
        ["glock"] = "Glock-18", ["hkp2000"] = "P2000", ["usp_silencer"] = "USP-S",
        ["p250"] = "P250", ["tec9"] = "Tec-9", ["cz75a"] = "CZ75-Auto",
        ["fiveseven"] = "Five-SeveN", ["elite"] = "Dual Berettas", ["deagle"] = "Desert Eagle",
        ["revolver"] = "R8 Revolver", ["nova"] = "Nova", ["xm1014"] = "XM1014",
        ["mag7"] = "MAG-7", ["sawedoff"] = "Sawed-Off", ["mp9"] = "MP9",
        ["mac10"] = "MAC-10", ["mp7"] = "MP7", ["mp5sd"] = "MP5-SD",
        ["ump45"] = "UMP-45", ["p90"] = "P90", ["bizon"] = "PP-Bizon",
        ["famas"] = "FAMAS", ["galilar"] = "Galil AR", ["m4a1"] = "M4A4",
        ["m4a1_silencer"] = "M4A1-S", ["ak47"] = "AK-47", ["sg556"] = "SG 553",
        ["aug"] = "AUG", ["ssg08"] = "SSG 08", ["awp"] = "AWP",
        ["g3sg1"] = "G3SG1", ["scar20"] = "SCAR-20", ["m249"] = "M249",
        ["negev"] = "Negev", ["taser"] = "Zeus x27", ["hegrenade"] = "HE Grenade",
        ["knife"] = "Knife",
    };

    private static readonly HashSet<string> PistolWeapons = new()
    {
        "glock", "hkp2000", "usp_silencer", "p250", "tec9", "cz75a",
        "fiveseven", "elite", "deagle", "revolver"
    };

    private string Prefix => $" {ChatColors.Orange}[GunGame]{ChatColors.Default}";

    public void OnConfigParsed(GunGameConfig config)
    {
        if (config.WeaponOrder.Count == 0)
        {
            Logger.LogWarning("WeaponOrder is empty, falling back to default order.");
            config.WeaponOrder = new GunGameConfig().WeaponOrder;
        }

        config.WeaponOrder = config.WeaponOrder
            .Select(w => w.Trim().ToLowerInvariant().Replace("weapon_", ""))
            .ToList();

        foreach (var weapon in config.WeaponOrder.Where(w => !WeaponDisplayNames.ContainsKey(w)))
            Logger.LogWarning("Unknown weapon '{Weapon}' in WeaponOrder - it will still be given as weapon_{Weapon}, make sure that entity exists.", weapon, weapon);

        if (config.KillsPerLevel < 1) config.KillsPerLevel = 1;
        if (config.WinnerDelay < 0) config.WinnerDelay = 0;
        config.SoundVolume = Math.Clamp(config.SoundVolume, 0f, 1f);

        Config = config;
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            ResetGame();
            ScheduleStartupRedirect();
            // Delayed so it runs after the gamemode cfgs reset bot_quota
            AddTimer(5.0f, ApplyBotFill);
        });
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            foreach (var file in Config.PrecacheSoundEventFiles.Where(f => !string.IsNullOrWhiteSpace(f)))
                manifest.AddResource(file);
        });
        RegisterListener<Listeners.OnTick>(OnTick);
        AddCommandListener("say", OnPlayerChat, HookMode.Pre);
        AddCommandListener("say_team", OnPlayerChat, HookMode.Pre);
        LoadMutedPlayers();

        if (hotReload)
        {
            _startupRedirectDone = true; // don't yank the map mid-session on plugin reload
            ResetGame();
            ApplyBotFill();
        }
    }

    private void ApplyBotFill()
    {
        if (!Config.Enabled || !Config.BotAutoFill)
            return;

        var mode = string.Equals(Config.BotQuotaMode, "normal", StringComparison.OrdinalIgnoreCase) ? "normal" : "fill";
        var slots = Config.BotAutoFillSlots > 0 ? Config.BotAutoFillSlots : Server.MaxPlayers;
        Server.ExecuteCommand($"bot_quota_mode {mode}");
        Server.ExecuteCommand($"bot_quota {slots}");
    }

    /// <summary>
    /// +host_workshop_map hangs at boot because Steam isn't connected yet, so the
    /// server launches on a stock map instead and we hop to the configured workshop
    /// map once, shortly after the Steam connection is up.
    /// </summary>
    private void ScheduleStartupRedirect()
    {
        if (_startupRedirectDone || string.IsNullOrWhiteSpace(Config.StartupWorkshopMap))
            return;

        _startupRedirectDone = true;
        var entry = Config.StartupWorkshopMap;
        AddTimer(10.0f, () =>
        {
            Logger.LogInformation("Switching to startup workshop map: {Entry}", entry);
            Server.ExecuteCommand(MapChangeCommand(entry));
        });
    }

    // ---------------------------------------------------------------- events

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var data in _players.Values)
            data.LevelsThisRound = 0;
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Config.Enabled || !IsValidPlayer(player))
            return HookResult.Continue;

        // Give the level weapon one frame later so it survives default loadout logic.
        // During the win countdown, freeze respawners instead.
        var slot = player!.Slot;
        AddTimer(0.10f, () =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !IsValidPlayer(p) || !p.PawnIsAlive)
                return;
            if (_winnerDeclared)
            {
                FreezePlayer(p);
            }
            else
            {
                GiveLevelWeapon(p);
                UpdateLeaderGlows();
            }
        });
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!Config.Enabled || _winnerDeclared)
            return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;
        var weapon = NormalizeWeapon(@event.Weapon);

        if (!IsValidPlayer(victim))
            return HookResult.Continue;

        RemoveGlow(victim!.Slot);

        if (IsWarmup() && !Config.CountWarmupKills)
            return HookResult.Continue;

        // Suicide / world death
        if (!IsValidPlayer(attacker) || attacker == victim)
        {
            if (Config.SuicidePenalty)
                LevelDown(victim!, "suicide");
            return HookResult.Continue;
        }

        // Team kill (ignored entirely in FFA)
        if (attacker!.TeamNum == victim!.TeamNum && !IsFriendlyFireAllowed())
        {
            if (Config.TeamKillPenalty)
                LevelDown(attacker, "team kill");
            return HookResult.Continue;
        }

        var data = GetData(attacker);
        var levelWeapon = WeaponAtLevel(data.Level);
        var knifeKill = IsKnife(weapon);

        // Knife steal (KnifePro) - knife kill while NOT on the knife level
        if (knifeKill && levelWeapon != "knife")
        {
            if (Config.KnifeSteal
                && (levelWeapon != "hegrenade" || Config.KnifeStealOnGrenadeLevel))
            {
                var victimData = GetData(victim);
                if (victimData.Level > Config.KnifeStealMinLevel)
                {
                    victimData.Level--;
                    victimData.Kills = 0;
                    if (!(attacker.IsBot && victim.IsBot))
                    {
                        Server.PrintToChatAll(
                            $"{Prefix} {ChatColors.Red}{attacker.PlayerName}{ChatColors.Default} stole a level from " +
                            $"{ChatColors.Blue}{victim.PlayerName}{ChatColors.Default} with a knife!");
                    }
                    PlaySound(attacker, Config.KnifeStealSound);
                    PlaySound(victim, Config.LevelDownSound);
                    LevelUp(attacker, data);
                }
                else
                {
                    attacker.PrintToChat($"{Prefix} {victim.PlayerName} is too low a level to steal from.");
                }
            }
            return HookResult.Continue;
        }

        // Normal progression: the kill only counts with the current level weapon
        // (any knife counts when on the knife level).
        var matches = levelWeapon == "knife" ? knifeKill : weapon == levelWeapon;
        if (!matches)
            return HookResult.Continue;

        data.Kills++;
        var required = RequiredKills(data.Level);

        if (data.Kills >= required)
        {
            if (Config.MaxLevelsPerRound > 0 && data.LevelsThisRound >= Config.MaxLevelsPerRound)
            {
                data.Kills = required; // bank the kills, level next round
                attacker.PrintToChat($"{Prefix} You reached the max of {Config.MaxLevelsPerRound} levels this round - you level up next round.");
            }
            else
            {
                LevelUp(attacker, data);
            }
        }
        else
        {
            attacker.PrintToChat(
                $"{Prefix} {ChatColors.Green}{data.Kills}{ChatColors.Default}/{required} kills with {DisplayName(levelWeapon)}.");
        }

        // Fresh grenade after a successful HE kill so the level stays playable
        if (levelWeapon == "hegrenade" && Config.ReplenishGrenade && !_winnerDeclared && attacker.PawnIsAlive)
            attacker.GiveNamedItem("weapon_hegrenade");

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Config.Enabled || !Config.WelcomeMessage || !IsValidPlayer(player) || player!.IsBot)
            return HookResult.Continue;

        // Delayed so it lands after the loading screen, where players actually see chat
        var slot = player.Slot;
        AddTimer(8.0f, () =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !IsValidPlayer(p) || p.IsBot)
                return;

            p.PrintToChat($"{Prefix} {ChatColors.Green}Welcome to GunGame!{ChatColors.Default} Get kills to climb through all {Config.WeaponOrder.Count} weapons - finish with the knife to win!");
            p.PrintToChat($"{Prefix} Commands: {ChatColors.Gold}!gg{ChatColors.Default} your level, {ChatColors.Gold}!ggtop{ChatColors.Default} leaders, {ChatColors.Gold}!ggsound{ChatColors.Default} mute all kill sounds");
            p.PrintToChat($"{Prefix} {ChatColors.Red}Knife kills steal a level{ChatColors.Default} from the victim!");
            p.PrintToChat($"{Prefix} Tip: console {ChatColors.LightBlue}snd_deathcamera_volume 0{ChatColors.Default} mutes the death camera jingle.");
        });
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
            RemoveGlow(player.Slot);
        Server.NextFrame(UpdateLeaderGlows);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info) =>
        GrantObjectiveBonus(@event.Userid);

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info) =>
        GrantObjectiveBonus(@event.Userid);

    private HookResult GrantObjectiveBonus(CCSPlayerController? player)
    {
        if (!Config.Enabled || _winnerDeclared || Config.ObjectiveBonus <= 0 || !IsValidPlayer(player))
            return HookResult.Continue;

        var data = GetData(player!);
        for (var i = 0; i < Config.ObjectiveBonus && !_winnerDeclared; i++)
        {
            // Objective bonus never wins the game, matching GunGame:SM's default
            if (data.Level >= Config.WeaponOrder.Count)
                break;
            LevelUp(player!, data);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        if (!Config.Enabled || !Config.ReplenishGrenade || _winnerDeclared)
            return HookResult.Continue;

        var player = @event.Userid;
        if (!IsValidPlayer(player) || !player!.PawnIsAlive)
            return HookResult.Continue;

        if (WeaponAtLevel(GetData(player).Level) != "hegrenade")
            return HookResult.Continue;

        var slot = player.Slot;
        AddTimer(1.0f, () =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null && IsValidPlayer(p) && p.PawnIsAlive && !_winnerDeclared
                && WeaponAtLevel(GetData(p).Level) == "hegrenade")
            {
                p.GiveNamedItem("weapon_hegrenade");
            }
        });
        return HookResult.Continue;
    }

    // ------------------------------------------------------------- gameplay

    private void LevelUp(CCSPlayerController player, PlayerData data)
    {
        data.Level++;
        data.Kills = 0;
        data.LevelsThisRound++;

        if (data.Level > Config.WeaponOrder.Count)
        {
            if (player.IsBot && !Config.BotsCanWin)
            {
                data.Level = Config.WeaponOrder.Count; // park the bot on the last level
                data.Kills = 0;
                Server.PrintToChatAll($"{Prefix} BOT {player.PlayerName} finished the weapon order, but bots cannot win.");
                return;
            }

            DeclareWinner(player);
            return;
        }

        var weapon = WeaponAtLevel(data.Level);
        // Bots level up silently - their chat spam drowns out the humans
        if (!player.IsBot)
        {
            Server.PrintToChatAll(
                $"{Prefix} {ChatColors.Green}{player.PlayerName}{ChatColors.Default} is now on level " +
                $"{ChatColors.Green}{data.Level}{ChatColors.Default}/{Config.WeaponOrder.Count} ({DisplayName(weapon)})");
        }
        PlaySound(player, Config.LevelUpSound);

        if (data.Level == Config.WeaponOrder.Count)
        {
            Server.PrintToChatAll($"{Prefix} {ChatColors.Red}{player.PlayerName} is on the FINAL level!{ChatColors.Default}");
            foreach (var p in Utilities.GetPlayers().Where(p => IsValidPlayer(p) && !p.IsBot))
                PlaySound(p, Config.FinalLevelSound);
        }

        if (Config.TurboMode && player.PawnIsAlive)
            GiveLevelWeapon(player);

        UpdateLeaderGlows();
    }

    private void LevelDown(CCSPlayerController player, string reason)
    {
        var data = GetData(player);
        if (data.Level <= 1)
            return;

        data.Level--;
        data.Kills = 0;
        player.PrintToChat(
            $"{Prefix} {ChatColors.Red}You lost a level ({reason}).{ChatColors.Default} " +
            $"Now level {data.Level} ({DisplayName(WeaponAtLevel(data.Level))}).");
        PlaySound(player, Config.LevelDownSound);

        if (Config.TurboMode && player.PawnIsAlive)
            GiveLevelWeapon(player);

        UpdateLeaderGlows();
    }

    private void DeclareWinner(CCSPlayerController player)
    {
        _winnerDeclared = true;

        Server.PrintToChatAll($"{Prefix} {ChatColors.Gold}*** {player.PlayerName} HAS WON THE GUNGAME! ***{ChatColors.Default}");
        RemoveAllGlows();
        FreezeAllPlayers();
        foreach (var p in Utilities.GetPlayers().Where(p => IsValidPlayer(p) && !p.IsBot))
            PlaySound(p, Config.WinnerSound);

        var seconds = Math.Max(1, (int)Math.Round(Config.WinnerDelay));
        _winnerName = player.PlayerName;
        _winEndsAt = Server.CurrentTime + seconds;

        StartMapVote();
        // The win card itself is drawn every tick in OnTick so other plugins'
        // center messages (e.g. the knife-kill announcer) can't replace it.
        AddTimer(seconds, EndGame);
    }

    private void StartMapVote()
    {
        _voteOptions.Clear();
        _mapVotes.Clear();
        _voteActive = false;

        // No point voting when WinnerCommand overrides the map change
        if (!Config.MapVoteEnabled || !string.IsNullOrWhiteSpace(Config.WinnerCommand))
            return;

        var candidates = CycleCandidates();
        if (candidates.Count < 2)
            return;

        var optionCount = Math.Clamp(Config.MapVoteOptions, 2, 5);
        _voteOptions = candidates
            .OrderBy(_ => Random.Shared.Next())
            .Take(optionCount)
            .ToList();
        _voteActive = true;

        Server.PrintToChatAll($"{Prefix} {ChatColors.Green}Vote for the next map!{ChatColors.Default} Type the number in chat:");
        for (var i = 0; i < _voteOptions.Count; i++)
            Server.PrintToChatAll($"{Prefix} {ChatColors.Gold}{i + 1}.{ChatColors.Default} {MapDisplayName(_voteOptions[i])}");
    }

    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (!_voteActive || player == null || !IsValidPlayer(player) || player.IsBot)
            return HookResult.Continue;

        var text = info.GetArg(1).Trim().TrimStart('!');
        if (!int.TryParse(text, out var choice) || choice < 1 || choice > _voteOptions.Count)
            return HookResult.Continue;

        _mapVotes[player.SteamID] = choice - 1;
        player.PrintToChat($"{Prefix} Vote registered: {ChatColors.Green}{MapDisplayName(_voteOptions[choice - 1])}{ChatColors.Default}");
        return HookResult.Handled; // keep vote numbers out of public chat
    }

    private string BuildWinnerHtml(int remaining)
    {
        var html = $"<font class='fontSize-l' color='#FFD700'>{_winnerName} wins the GunGame!</font>";

        if (_voteActive)
        {
            html += "<br><font class='fontSize-m' color='#FFFFFF'>Vote for the next map - type the number in chat:</font>";
            for (var i = 0; i < _voteOptions.Count; i++)
            {
                var votes = _mapVotes.Values.Count(v => v == i);
                html += $"<br><font color='#87CEEB'>{i + 1}. {MapDisplayName(_voteOptions[i])}</font> <font color='#FFD700'>({votes})</font>";
            }
        }

        html += $"<br><font class='fontSize-m' color='#FFFFFF'>Map change in {remaining}...</font>";
        return html;
    }

    private void EndGame()
    {
        var cmd = Config.WinnerCommand;
        if (string.IsNullOrWhiteSpace(cmd))
        {
            string? chosen = null;
            if (_voteActive)
            {
                _voteActive = false;
                chosen = ResolveVote();
                if (chosen != null)
                    Server.PrintToChatAll($"{Prefix} Next map: {ChatColors.Green}{MapDisplayName(chosen)}{ChatColors.Default}");
            }

            chosen ??= PickRandomFromCycle();
            if (chosen != null)
            {
                _lastMapCycleEntry = chosen.Trim();
                cmd = MapChangeCommand(chosen);
            }
            else
            {
                cmd = "mp_restartgame 1";
            }
        }

        ResetGame();
        Server.ExecuteCommand(cmd);
    }

    private string? ResolveVote()
    {
        if (_voteOptions.Count == 0 || _mapVotes.Count == 0)
            return null; // nobody voted - fall back to random

        var best = _voteOptions
            .Select((entry, i) => (Entry: entry, Votes: _mapVotes.Values.Count(v => v == i)))
            .GroupBy(x => x.Votes)
            .OrderByDescending(g => g.Key)
            .First()
            .ToList();
        return best[Random.Shared.Next(best.Count)].Entry; // random among tied leaders
    }

    private List<string> CycleCandidates()
    {
        var current = Server.MapName;
        var candidates = Config.MapCycle
            .Where(m => !string.IsNullOrWhiteSpace(m)
                && !MapCycleName(m).Equals(current, StringComparison.OrdinalIgnoreCase)
                && !m.Trim().Equals(_lastMapCycleEntry, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
            candidates = Config.MapCycle.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        return candidates;
    }

    private string? PickRandomFromCycle()
    {
        var candidates = CycleCandidates();
        return candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : null;
    }

    private string MapDisplayName(string entry)
    {
        entry = entry.Trim();
        if (Config.MapNames.TryGetValue(entry, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        return MapCycleName(entry);
    }

    /// <summary>
    /// MapCycle entries: "de_dust2" (stock map), "ws:gg_arena" (workshop map
    /// from the loaded collection by name), or "3070284539" (workshop file id).
    /// </summary>
    private static string MapChangeCommand(string entry)
    {
        entry = entry.Trim();
        if (entry.Length > 0 && entry.All(char.IsDigit))
            return $"host_workshop_map {entry}";
        if (entry.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
            return $"ds_workshop_changelevel {entry[3..].Trim()}";
        return $"changelevel {entry}";
    }

    private static string MapCycleName(string entry)
    {
        entry = entry.Trim();
        return entry.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ? entry[3..].Trim() : entry;
    }

    private void FreezeAllPlayers()
    {
        foreach (var p in Utilities.GetPlayers().Where(IsValidPlayer))
            FreezePlayer(p);
    }

    private static void FreezePlayer(CCSPlayerController player)
    {
        if (!player.PawnIsAlive)
            return;

        player.RemoveWeapons();
        var pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
        Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", (int)MoveType_t.MOVETYPE_OBSOLETE);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }

    private void GiveLevelWeapon(CCSPlayerController player)
    {
        var data = GetData(player);
        var weapon = WeaponAtLevel(data.Level);

        player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
        if (Config.GiveArmor)
            player.GiveNamedItem("item_assaultsuit");

        if (weapon != "knife")
            player.GiveNamedItem($"weapon_{weapon}");

        // Best-effort switch to the level weapon
        var slotCmd = weapon == "knife" ? "slot3"
            : weapon == "hegrenade" ? "slot4"
            : PistolWeapons.Contains(weapon) ? "slot2"
            : "slot1";
        player.ExecuteClientCommand(slotCmd);
    }

    private void ResetGame()
    {
        RemoveAllGlows();
        _players.Clear();
        _mapVotes.Clear();
        _voteOptions.Clear();
        _voteActive = false;
        _winnerDeclared = false;
        _winnerName = null;
        _gameRules = null;
        _ffaConVar = null;
    }

    // ------------------------------------------------------------ leader glow

    private static readonly Color LeaderTint = Color.FromArgb(255, 90, 255, 90);

    /// <summary>
    /// The leader is marked by tinting their player model green. CS2's glow
    /// overlay ignores its occlusion flags (always shows through walls), so a
    /// model tint is the reliable way to make this line-of-sight only.
    /// </summary>
    private void UpdateLeaderGlows()
    {
        if (!Config.Enabled || !Config.LeaderGlowEnabled || _winnerDeclared)
            return;

        var players = Utilities.GetPlayers()
            .Where(p => IsValidPlayer(p) && p.TeamNum >= (byte)CsTeam.Terrorist)
            .ToList();
        var leaderLevel = players.Count > 0 ? players.Max(p => GetData(p).Level) : 0;

        foreach (var p in players)
        {
            // Nobody glows while everyone is still on level 1
            var shouldGlow = leaderLevel > 1 && GetData(p).Level == leaderLevel && p.PawnIsAlive;
            var hasGlow = _tintedSlots.Contains(p.Slot);

            if (shouldGlow && !hasGlow)
            {
                SetPawnTint(p, LeaderTint);
                _tintedSlots.Add(p.Slot);
            }
            else if (!shouldGlow && hasGlow)
            {
                RemoveGlow(p.Slot);
            }
        }
    }

    private static void SetPawnTint(CCSPlayerController player, Color color)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        pawn.Render = color;
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }

    private void RemoveGlow(int slot)
    {
        if (!_tintedSlots.Remove(slot))
            return;

        var player = Utilities.GetPlayerFromSlot(slot);
        if (player != null && player.IsValid)
            SetPawnTint(player, Color.White);
    }

    private void RemoveAllGlows()
    {
        foreach (var slot in _tintedSlots.ToList())
            RemoveGlow(slot);
    }

    // -------------------------------------------------------------- helpers

    private PlayerData GetData(CCSPlayerController player)
    {
        var key = PlayerKey(player);
        if (_players.TryGetValue(key, out var data))
            return data;

        data = new PlayerData();

        // Handicap: late joiners start at the average level of current players
        if (Config.HandicapMode && _players.Count > 0)
        {
            var avg = (int)_players.Values.Average(d => d.Level);
            data.Level = Math.Max(1, avg);
        }

        _players[key] = data;
        return data;
    }

    private static ulong PlayerKey(CCSPlayerController player) =>
        player.IsBot ? 1_000_000UL + (ulong)player.Slot : player.SteamID;

    private string WeaponAtLevel(int level) =>
        Config.WeaponOrder[Math.Clamp(level, 1, Config.WeaponOrder.Count) - 1];

    private int RequiredKills(int level) =>
        Config.KillsPerLevelOverride.TryGetValue(level.ToString(), out var kills) && kills > 0
            ? kills
            : Config.KillsPerLevel;

    private static string DisplayName(string weapon) =>
        WeaponDisplayNames.GetValueOrDefault(weapon, weapon.ToUpperInvariant());

    private static string NormalizeWeapon(string weapon)
    {
        weapon = weapon.ToLowerInvariant();
        return weapon.StartsWith("weapon_") ? weapon["weapon_".Length..] : weapon;
    }

    private static bool IsKnife(string weapon) =>
        weapon.Contains("knife") || weapon.Contains("bayonet");

    private static bool IsValidPlayer(CCSPlayerController? player) =>
        player != null && player.IsValid && !player.IsHLTV
        && player.Connected == PlayerConnectedState.Connected;

    private bool IsWarmup()
    {
        _gameRules ??= Utilities
            .FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
            .FirstOrDefault()?.GameRules;
        return _gameRules?.WarmupPeriod ?? false;
    }

    private bool IsFriendlyFireAllowed()
    {
        _ffaConVar ??= ConVar.Find("mp_teammates_are_enemies");
        return _ffaConVar?.GetPrimitiveValue<bool>() ?? false;
    }

    /// <summary>
    /// Sounds containing a path separator are client sound files played via
    /// play/playvol. Anything else (e.g. "QuakeSoundsD.Humiliation") is treated
    /// as a sound event, which requires the addon providing it to be mounted
    /// (see MultiAddonManager + mm_extra_addons).
    /// </summary>
    private void PlaySound(CCSPlayerController player, string sound)
    {
        if (string.IsNullOrWhiteSpace(sound) || player.IsBot || _soundMuted.Contains(player.SteamID))
            return;

        if (sound.Contains('/') || sound.Contains('\\'))
        {
            if (Config.SoundVolume >= 0.99f)
            {
                player.ExecuteClientCommand($"play {sound}");
            }
            else
            {
                var vol = Config.SoundVolume.ToString("0.##", CultureInfo.InvariantCulture);
                player.ExecuteClientCommand($"playvol {sound} {vol}");
            }
        }
        else
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null)
                return;
            var filter = new RecipientFilter(player);
            pawn.EmitSound(sound, filter, Config.SoundVolume);
        }
    }

    private string MutedPlayersPath => Path.Combine(ModuleDirectory, "muted_players.json");

    private void LoadMutedPlayers()
    {
        try
        {
            if (!File.Exists(MutedPlayersPath))
                return;
            var ids = JsonSerializer.Deserialize<List<ulong>>(File.ReadAllText(MutedPlayersPath));
            _soundMuted.Clear();
            if (ids != null)
                foreach (var id in ids)
                    _soundMuted.Add(id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not load muted players list: {Error}", ex.Message);
        }
    }

    private void SaveMutedPlayers()
    {
        try
        {
            File.WriteAllText(MutedPlayersPath, JsonSerializer.Serialize(_soundMuted.ToList()));
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not save muted players list: {Error}", ex.Message);
        }
    }

    // ------------------------------------------------------------------ hud

    private void OnTick()
    {
        if (!Config.Enabled)
            return;

        // Win freeze: keep the winner/vote card on screen every tick so other
        // plugins' center messages can't replace it.
        if (_winnerDeclared)
        {
            if (_winnerName == null)
                return;
            var remaining = Math.Max(0, (int)Math.Ceiling(_winEndsAt - Server.CurrentTime));
            var winnerHtml = BuildWinnerHtml(remaining);
            foreach (var p in Utilities.GetPlayers().Where(p => IsValidPlayer(p) && !p.IsBot))
                p.PrintToCenterHtml(winnerHtml);
            return;
        }

        if (!Config.HudEnabled)
            return;

        // Find the current leader once per tick
        CCSPlayerController? leader = null;
        var leaderLevel = 0;
        var players = Utilities.GetPlayers();
        foreach (var p in players)
        {
            if (!IsValidPlayer(p) || p.TeamNum < (byte)CsTeam.Terrorist)
                continue;
            var level = GetData(p).Level;
            if (level > leaderLevel)
            {
                leaderLevel = level;
                leader = p;
            }
        }

        foreach (var p in players)
        {
            if (!IsValidPlayer(p) || p.IsBot || p.TeamNum < (byte)CsTeam.Terrorist)
                continue;

            var data = GetData(p);
            var weapon = WeaponAtLevel(data.Level);
            var required = RequiredKills(data.Level);

            var html =
                $"<font color='#FFA500'>Level {data.Level}/{Config.WeaponOrder.Count}</font> " +
                $"<font color='#FFFFFF'>{DisplayName(weapon)} ({data.Kills}/{required})</font>";
            if (leader != null && leader != p)
                html += $"<br><font color='#87CEEB'>Leader: {leader.PlayerName} (Lv {leaderLevel})</font>";
            else if (leader == p)
                html += "<br><font color='#FFD700'>You are the leader!</font>";

            p.PrintToCenterHtml(html);
        }
    }

    // ------------------------------------------------------------- commands

    [ConsoleCommand("css_gg", "Show your current GunGame level")]
    [ConsoleCommand("css_level", "Show your current GunGame level")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnLevelCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;
        var data = GetData(player);
        player.PrintToChat(
            $"{Prefix} You are level {ChatColors.Green}{data.Level}{ChatColors.Default}/{Config.WeaponOrder.Count} " +
            $"({DisplayName(WeaponAtLevel(data.Level))}) - {data.Kills}/{RequiredKills(data.Level)} kills.");
    }

    [ConsoleCommand("css_ggtop", "Show the GunGame leaders")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnTopCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        var ranked = Utilities.GetPlayers()
            .Where(p => IsValidPlayer(p) && p.TeamNum >= (byte)CsTeam.Terrorist)
            .Select(p => (Player: p, GetData(p).Level))
            .OrderByDescending(x => x.Level)
            .Take(3)
            .ToList();

        if (ranked.Count == 0)
        {
            player.PrintToChat($"{Prefix} Nobody is playing yet.");
            return;
        }

        player.PrintToChat($"{Prefix} {ChatColors.Gold}Top players:{ChatColors.Default}");
        for (var i = 0; i < ranked.Count; i++)
            player.PrintToChat($"{Prefix} #{i + 1} {ranked[i].Player.PlayerName} - level {ranked[i].Level}");
    }

    [ConsoleCommand("css_ggsound", "Toggle GunGame sounds on/off for yourself")]
    [ConsoleCommand("css_ggsounds", "Toggle GunGame sounds on/off for yourself")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnSoundCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (_soundMuted.Remove(player.SteamID))
        {
            player.PrintToChat($"{Prefix} GunGame + announcer sounds {ChatColors.Green}enabled{ChatColors.Default}. Use !ggsound to mute them again.");
        }
        else
        {
            _soundMuted.Add(player.SteamID);
            player.PrintToChat($"{Prefix} GunGame + announcer sounds {ChatColors.Red}muted{ChatColors.Default}. Use !ggsound to re-enable them.");
        }
        SaveMutedPlayers();

        // One command to rule them all: also flip the QuakeSounds plugin's own
        // per-player toggle (registered under its raw config name, e.g. "qs",
        // NOT css_qs). It keeps its own saved state, so if the two ever drift
        // out of sync, a lone !qs re-aligns the announcer.
        if (!string.IsNullOrWhiteSpace(Config.AnnouncerMuteCommand))
            player.ExecuteClientCommandFromServer(Config.AnnouncerMuteCommand);
    }

    [ConsoleCommand("css_ggreset", "Reset all GunGame levels")]
    [RequiresPermissions("@css/generic")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnResetCommand(CCSPlayerController? player, CommandInfo command)
    {
        foreach (var data in _players.Values)
        {
            data.Level = 1;
            data.Kills = 0;
            data.LevelsThisRound = 0;
        }
        _winnerDeclared = false;

        RemoveAllGlows();
        foreach (var p in Utilities.GetPlayers().Where(p => IsValidPlayer(p) && p.PawnIsAlive))
            GiveLevelWeapon(p);

        Server.PrintToChatAll($"{Prefix} All levels have been reset by an admin.");
    }
}
