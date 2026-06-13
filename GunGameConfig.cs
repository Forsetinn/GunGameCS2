using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GunGameCS2;

public class GunGameConfig : BasePluginConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The weapon progression, level 1 first. Use CS2 short weapon names
    /// (the same names the kill feed uses): glock, hkp2000, usp_silencer, p250,
    /// tec9, cz75a, fiveseven, elite, deagle, revolver, nova, xm1014, mag7,
    /// sawedoff, mp9, mac10, mp7, mp5sd, ump45, p90, bizon, famas, galilar,
    /// m4a1, m4a1_silencer, ak47, sg556, aug, ssg08, awp, g3sg1, scar20,
    /// m249, negev, taser, hegrenade, knife
    /// </summary>
    [JsonPropertyName("WeaponOrder")]
    public List<string> WeaponOrder { get; set; } = new()
    {
        "glock",
        "usp_silencer",
        "hkp2000",
        "p250",
        "tec9",
        "elite",
        "fiveseven",
        "deagle",
        "revolver",
        "cz75a",
        "nova",
        "xm1014",
        "mag7",
        "mp9",
        "mac10",
        "mp7",
        "ump45",
        "p90",
        "bizon",
        "famas",
        "galilar",
        "m4a1_silencer",
        "m4a1",
        "ak47",
        "sg556",
        "aug",
        "ssg08",
        "awp",
        "scar20",
        "g3sg1",
        "m249",
        "negev",
        "taser",
        "hegrenade",
        "knife"
    };

    /// <summary>Default kills required to advance one level.</summary>
    [JsonPropertyName("KillsPerLevel")]
    public int KillsPerLevel { get; set; } = 2;

    /// <summary>
    /// Per-level override of KillsPerLevel. Levels not listed use KillsPerLevel.
    /// Defaults: SMGs/rifles need 3 kills, snipers/Zeus/nade/knife are quick
    /// 1-kill levels at the end of the run.
    /// </summary>
    [JsonPropertyName("KillsPerLevelOverride")]
    public Dictionary<string, int> KillsPerLevelOverride { get; set; } = new()
    {
        ["10"] = 1,             // cz75a
        ["14"] = 3,             // mp9
        ["15"] = 3,             // mac10
        ["16"] = 3,             // mp7
        ["17"] = 3,             // ump45
        ["18"] = 3,             // p90
        ["19"] = 3,             // bizon
        ["20"] = 3,             // famas
        ["21"] = 3,             // galilar
        ["22"] = 3,             // m4a1_silencer
        ["23"] = 3,             // m4a1
        ["24"] = 3,             // ak47
        ["25"] = 3,             // sg556
        ["26"] = 3,             // aug
        ["29"] = 1,             // scar20
        ["30"] = 1,             // g3sg1
        ["31"] = 3,             // m249
        ["32"] = 3,             // negev
        ["33"] = 1,             // taser
        ["34"] = 1,             // hegrenade
        ["35"] = 1,             // knife
    };

    /// <summary>Give the new weapon immediately on level up (Arms Race style). When off, the new weapon comes on next spawn.</summary>
    [JsonPropertyName("TurboMode")]
    public bool TurboMode { get; set; } = true;

    /// <summary>Knife kills steal a level from the victim and advance the killer (KnifePro).</summary>
    [JsonPropertyName("KnifeSteal")]
    public bool KnifeSteal { get; set; } = true;

    /// <summary>Victims at or below this level cannot have levels stolen from them.</summary>
    [JsonPropertyName("KnifeStealMinLevel")]
    public int KnifeStealMinLevel { get; set; } = 2;

    /// <summary>Allow knife stealing while the killer is on the grenade level (KnifeProHE in GunGame:SM).</summary>
    [JsonPropertyName("KnifeStealOnGrenadeLevel")]
    public bool KnifeStealOnGrenadeLevel { get; set; } = false;

    /// <summary>Levels awarded for planting or defusing the bomb. 0 = disabled.</summary>
    [JsonPropertyName("ObjectiveBonus")]
    public int ObjectiveBonus { get; set; } = 0;

    /// <summary>Lose a level when you kill yourself (kill command, fall damage, world, own grenade).</summary>
    [JsonPropertyName("SuicidePenalty")]
    public bool SuicidePenalty { get; set; } = true;

    /// <summary>Lose a level when you kill a teammate (only matters when FFA is off).</summary>
    [JsonPropertyName("TeamKillPenalty")]
    public bool TeamKillPenalty { get; set; } = false;

    /// <summary>Maximum levels a player can gain in a single round. 0 = unlimited.</summary>
    [JsonPropertyName("MaxLevelsPerRound")]
    public int MaxLevelsPerRound { get; set; } = 0;

    /// <summary>Late joiners start at the average level of everyone already playing.</summary>
    [JsonPropertyName("HandicapMode")]
    public bool HandicapMode { get; set; } = false;

    /// <summary>Allow bots to win the game. When off, a bot stays parked on the last level.</summary>
    [JsonPropertyName("BotsCanWin")]
    public bool BotsCanWin { get; set; } = false;

    /// <summary>Keep bots on the server (see BotQuotaMode + BotAutoFillSlots).</summary>
    [JsonPropertyName("BotAutoFill")]
    public bool BotAutoFill { get; set; } = true;

    /// <summary>
    /// "fill": BotAutoFillSlots is the TOTAL population (humans + bots); a bot
    /// leaves whenever a human joins. "normal": BotAutoFillSlots is a constant
    /// number of bots regardless of humans.
    /// </summary>
    [JsonPropertyName("BotQuotaMode")]
    public string BotQuotaMode { get; set; } = "fill";

    /// <summary>Meaning depends on BotQuotaMode (total to maintain, or bot count). 0 = server max slots.</summary>
    [JsonPropertyName("BotAutoFillSlots")]
    public int BotAutoFillSlots { get; set; } = 0;

    /// <summary>Give a fresh HE grenade after the previous one detonates while on the grenade level.</summary>
    [JsonPropertyName("ReplenishGrenade")]
    public bool ReplenishGrenade { get; set; } = true;

    /// <summary>Give kevlar + helmet on spawn.</summary>
    [JsonPropertyName("GiveArmor")]
    public bool GiveArmor { get; set; } = true;

    /// <summary>Show the always-on center HUD with level / kills / leader.</summary>
    [JsonPropertyName("HudEnabled")]
    public bool HudEnabled { get; set; } = true;

    /// <summary>Greet joining players with the rules, chat commands and the deathcam-mute tip.</summary>
    [JsonPropertyName("WelcomeMessage")]
    public bool WelcomeMessage { get; set; } = true;

    /// <summary>Color the player(s) on the highest level bright green (model tint - visible only in line of sight). Ties all glow.</summary>
    [JsonPropertyName("LeaderGlowEnabled")]
    public bool LeaderGlowEnabled { get; set; } = true;

    /// <summary>Count kills made during the warmup period.</summary>
    [JsonPropertyName("CountWarmupKills")]
    public bool CountWarmupKills { get; set; } = false;

    /// <summary>Seconds the match stays frozen (with countdown + map vote) before the map changes.</summary>
    [JsonPropertyName("WinnerDelay")]
    public float WinnerDelay { get; set; } = 15.0f;

    /// <summary>Let players vote on the next map during the win freeze (type 1-N in chat). Falls back to random when off or fewer than 2 candidates.</summary>
    [JsonPropertyName("MapVoteEnabled")]
    public bool MapVoteEnabled { get; set; } = true;

    /// <summary>How many maps appear in the vote (2-5).</summary>
    [JsonPropertyName("MapVoteOptions")]
    public int MapVoteOptions { get; set; } = 3;

    /// <summary>Display names for MapCycle entries, e.g. { "3111189015": "gg_simpsons_dust" }. Entries without a name show the raw entry.</summary>
    [JsonPropertyName("MapNames")]
    public Dictionary<string, string> MapNames { get; set; } = new();

    /// <summary>
    /// Server command executed when the countdown ends. Leave empty to change
    /// to a random map from MapCycle. Examples: "mp_restartgame 1" to restart
    /// the same map, or "changelevel de_mirage" for a fixed next map.
    /// </summary>
    [JsonPropertyName("WinnerCommand")]
    public string WinnerCommand { get; set; } = "";

    /// <summary>
    /// Workshop map (file id or ws:name) to switch to shortly after server boot.
    /// Works around +host_workshop_map hanging at startup before Steam connects.
    /// Empty = stay on the launch map.
    /// </summary>
    [JsonPropertyName("StartupWorkshopMap")]
    public string StartupWorkshopMap { get; set; } = "";

    /// <summary>Maps to rotate through after a win (used when WinnerCommand is empty). The current map is excluded from the pick.</summary>
    [JsonPropertyName("MapCycle")]
    public List<string> MapCycle { get; set; } = new()
    {
        "de_dust2", "de_mirage", "de_inferno", "de_nuke",
        "de_ancient", "de_overpass", "de_vertigo", "de_anubis"
    };

    /// <summary>
    /// Command !ggsound also triggers on the player's behalf, so one command
    /// mutes everything (set to the QuakeSounds settings command, default "qs").
    /// "" disables the forwarding.
    /// </summary>
    [JsonPropertyName("AnnouncerMuteCommand")]
    public string AnnouncerMuteCommand { get; set; } = "qs";

    /// <summary>Playback volume for all GunGame sounds, 0.0 - 1.0. Players can also mute them individually with !ggsound.</summary>
    [JsonPropertyName("SoundVolume")]
    public float SoundVolume { get; set; } = 0.5f;

    // Sounds are played per-client with the "play"/"playvol" console command.
    // Set any of them to "" to disable that sound.
    [JsonPropertyName("LevelUpSound")]
    public string LevelUpSound { get; set; } = "sounds/ui/achievement_earned.vsnd";

    [JsonPropertyName("LevelDownSound")]
    public string LevelDownSound { get; set; } = "sounds/ui/weapon_cant_buy.vsnd";

    [JsonPropertyName("KnifeStealSound")]
    public string KnifeStealSound { get; set; } = "sounds/ui/coin_pickup_01.vsnd";

    [JsonPropertyName("WinnerSound")]
    public string WinnerSound { get; set; } = "sounds/ui/xp_levelup.vsnd";

    /// <summary>Played to EVERYONE when a player reaches the final (knife) level. Sound event or file path, "" = off.</summary>
    [JsonPropertyName("FinalLevelSound")]
    public string FinalLevelSound { get; set; } = "";

    /// <summary>
    /// Soundevent files to precache on map load. Required for sound events from
    /// extra workshop addons mounted via MultiAddonManager (the engine only
    /// auto-precaches the map addon's own soundevents_addon.vsndevts).
    /// </summary>
    [JsonPropertyName("PrecacheSoundEventFiles")]
    public List<string> PrecacheSoundEventFiles { get; set; } = new();
}
