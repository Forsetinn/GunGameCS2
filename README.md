# GunGame CS2

A Counter-Strike 2 GunGame plugin built on [CounterStrikeSharp](https://docs.cssharp.dev/),
inspired by the classic **GunGame:SM** SourceMod plugin: kill with your current
weapon to advance through the weapon order, win with the final knife kill.

## Features

- **Weapon progression** — fully configurable order. Default is a 35-level run
  through the entire arsenal: all 10 pistols → shotguns → SMGs → rifles →
  snipers → machine guns → Zeus → HE grenade → knife. Kills with the wrong
  weapon don't count.
- **Kills per level** — global default (2) plus per-level overrides. Defaults:
  SMGs and rifles need 3 kills, and the final stretch (snipers, Zeus, HE,
  knife) are quick 1-kill levels.
- **Turbo mode** — new weapon handed over instantly on level up (Arms Race
  style). Off = next spawn.
- **Knife steal (KnifePro)** — knife kills steal a level from the victim, with a
  minimum-level guard and an option to block stealing while on the grenade level.
- **Leader glow** — the player(s) on the highest level are tinted bright green
  (model color, so strictly line-of-sight — no wallhack). Ties all glow.
  (A true outline glow was tried first, but CS2 ignores the glow occlusion
  flags and always renders outlines through walls.)
- **Win sequence** — on the winning kill everyone is instantly disarmed and
  frozen, then the next map loads after a center-screen countdown.
- **End-of-game map vote** — during the win freeze, players see random maps
  from the cycle with live vote counts and vote by typing the number in chat.
  Majority wins; ties or no votes fall back to random.
- **Map cycle with workshop support** — entries can be stock maps (`de_dust2`),
  workshop maps by name (`ws:gg_arena`), or workshop file IDs (`3111189015`).
  Never repeats the same map twice in a row.
- **Workshop startup map** — boots on a stock map (required for Steam to
  connect), then auto-switches to your configured workshop map.
- **Bot auto-fill** — keeps the server filled with bots; a bot leaves whenever a
  human joins (`bot_quota_mode fill`), reapplied every map change.
- **Sounds** — level up/down, knife steal and winner sounds. Each entry is
  either a built-in CS2 sound file (`sounds/ui/xp_levelup.vsnd`) or a sound
  event from a mounted workshop addon (`QuakeSoundsD.Excellent`). Global volume
  control, and players can mute them individually with `!ggsound` (persisted
  across restarts).
- **Center HUD** — always-on display of your level, kill progress and the
  current leader.
- **Classic extras** — suicide/team-kill penalty, max levels per round, handicap
  for late joiners, bots-can't-win toggle, HE replenish on the grenade level,
  objective bonus, warmup kill exclusion.
- **Levels persist** across reconnects within a map.

## Commands

| Command | Chat | Description |
|---|---|---|
| `css_gg` / `css_level` | `!gg`, `!level` | Show your current level |
| `css_ggtop` | `!ggtop` | Show the top 3 players |
| `css_ggsound` | `!ggsound` | Mute/unmute GunGame sounds for yourself (also flips the QuakeSounds `!qs` toggle) |
| `css_ggreset` | `!ggreset` | Reset all levels (admin: `@css/generic`) |

---

# Full server setup from scratch (Windows & Linux)

Renting a game server instead of self-hosting? Most panels (Pterodactyl etc.)
handle steps 1 and 6 for you — start at step 2 and upload files via SFTP.

## 1. Install SteamCMD + the CS2 dedicated server

**Windows:** download [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD),
extract e.g. to `D:\SteamCMD\Install`, then install the server (~35 GB):
```
D:\SteamCMD\Install\steamcmd.exe +force_install_dir D:\SteamCMD\CS2 +login anonymous +app_update 730 validate +quit
```

**Linux:**
```bash
mkdir -p ~/steamcmd ~/cs2 && cd ~/steamcmd
curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar xz
./steamcmd.sh +force_install_dir ~/cs2 +login anonymous +app_update 730 validate +quit
```

The same command (without `validate`) updates the server after CS2 patches.
**You must update the server every time CS2 updates**, or clients on the new
build can't join.

The game folder is `<install>/game/csgo`, referred to as `csgo/` below.

## 2. Install Metamod:Source

1. Download the latest **2.x dev build** for your OS from
   [sourcemm.net](https://www.sourcemm.net/downloads.php?branch=master)
   (Windows zip / Linux tar.gz).
2. Extract into `csgo/` (it adds `csgo/addons/metamod/...`).
3. Edit `csgo/gameinfo.gi`: inside `FileSystem > SearchPaths`, add this line
   **above** the `Game csgo` line:
   ```
   Game	csgo/addons/metamod
   ```
   Placement matters — if the line comes after the stock game paths, Metamod
   silently never loads. **CS2 updates rewrite this file**, so re-check it
   after every update (it's the #1 cause of "everything suddenly broke").
4. Verify later with `meta list` in the server console.

## 3. Install CounterStrikeSharp

1. Download the latest release **with runtime** for your OS from
   [CounterStrikeSharp releases](https://github.com/roflmuffin/CounterStrikeSharp/releases)
   (`...-with-runtime-windows-...` / `...-with-runtime-linux-...`).
2. Extract into `csgo/` (it adds `csgo/addons/counterstrikesharp/...` and a
   `counterstrikesharp.vdf` into `csgo/addons/metamod/`).
3. Verify later with `css_plugins list` in the server console.

Note: a wall of `Could not PreloadLibrary ... Access violation` lines at boot
is harmless — that's CS2's preloader tripping over .NET DLLs it doesn't manage.

## 4. Install the GunGame plugin

Copy from this project's `deploy/` folder into the server:

```
deploy/addons/counterstrikesharp/plugins/GunGameCS2/GunGameCS2.dll
        -> csgo/addons/counterstrikesharp/plugins/GunGameCS2/GunGameCS2.dll
deploy/cfg/gungame_server.cfg
        -> csgo/cfg/gungame_server.cfg
```

The plugin generates its config on first load at:
`csgo/addons/counterstrikesharp/configs/plugins/GunGameCS2/GunGameCS2.json`

Also create `csgo/cfg/gamemode_casual_server.cfg` containing just:
```
exec gungame_server.cfg
```
CS2 executes this file automatically *after* the casual gamemode defaults on
every map load — without it, the gamemode config overrides your settings
(buy time, round time, respawns, etc.).

## 5. Admins

Create `csgo/addons/counterstrikesharp/configs/admins.json`:
```json
{
  "YourName": {
    "identity": "76561198000000000",
    "flags": ["@css/root"]
  }
}
```
Use your steamID64. Reload with `css_admins_reload`.

## 6. Launch script

**Windows** — create `start_gungame.bat` next to the install:

```bat
@echo off
title CS2 GunGame Server
cd /d "D:\SteamCMD\CS2\game\bin\win64"

REM GSLT token (required for non-LAN play):
REM https://steamcommunity.com/dev/managegameservers (App ID 730)
set GSLT=+sv_setsteamaccount YOURTOKEN
REM Workshop collection (optional):
set COLLECTION=+host_workshop_collection YOUR_COLLECTION_ID

:serverloop
cs2.exe -dedicated -console -usercon -port 27015 ^
    +game_type 0 +game_mode 0 ^
    -maxplayers 16 ^
    +map de_dust2 ^
    +exec gungame_server.cfg ^
    %GSLT% %COLLECTION%

echo Server exited - restarting in 5 seconds (close window to stop)...
timeout /t 5
goto serverloop
```

**Linux** — create `start_gungame.sh` (`chmod +x` it):

```bash
#!/bin/bash
cd ~/cs2/game/bin/linuxsteamrt64

GSLT="+sv_setsteamaccount YOURTOKEN"
COLLECTION="+host_workshop_collection YOUR_COLLECTION_ID"

while true; do
    ./cs2 -dedicated -usercon -port 27015 \
        +game_type 0 +game_mode 0 \
        -maxplayers 16 \
        +map de_dust2 \
        +exec gungame_server.cfg \
        $GSLT $COLLECTION
    echo "Server exited - restarting in 5 seconds (Ctrl+C to stop)..."
    sleep 5
done
```

**Hosted (panel) servers:** set the same flags in the panel's startup
parameters / variables instead — map `de_dust2`, gamemode/gametype `0`,
your GSLT token, and the workshop collection ID.

**Important:** do NOT use `+host_workshop_map` as the launch map — it hangs the
server forever (the map fetch needs Steam, which only connects during the first
map load). Boot on a stock map and let the plugin's `StartupWorkshopMap` config
switch to your workshop map ~10 seconds later.

Without a GSLT token the server is LAN-only. For internet play also open
**UDP 27015**: on Windows forward it on your router and allow cs2.exe through
the firewall; on Linux e.g. `ufw allow 27015/udp`. Hosted servers handle this
for you (use the port your panel assigns).

## 7. Workshop maps

1. Make a workshop collection of your maps (visibility **Public** or
   **Unlisted** — Private can't be downloaded by the server).
2. Put the collection ID in the launch script (`%COLLECTION%`).
3. Set the plugin config: `StartupWorkshopMap` for the boot map, and `MapCycle`
   with your map IDs for the win rotation.
4. Console helpers: `ds_workshop_listmaps`, `ds_workshop_changelevel <name>`,
   `host_workshop_map <id>`.

Bots need a nav mesh baked into the map — on maps without one they stand still.

## 8. Custom sounds (optional)

- Install [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager)
  (extract into `csgo/`, set addon IDs in
  `csgo/cfg/multiaddonmanager/multiaddonmanager.cfg` → `mm_extra_addons`).
  Clients auto-download the mounted addons on connect.
- Quake announcer: install [cs2-quake-sounds](https://github.com/Kandru/cs2-quake-sounds)
  and mount its sound addon `3461824328`. Configure which events play in its
  JSON config (`sound_hearable_by: "attacker"` keeps it client-sided).
- GunGame level sounds: a ready-made addon ships with this project — mount
  workshop ID `3742790165` and set `"LevelUpSound": "GunGame.LevelUp"` etc.
  See `GunGameSoundsAddon/README.md` for the drag-and-drop setup, or the same
  doc's guide to building an addon with your own audio.

## 9. Verify

Start the server, then in its console:
- `meta list` → shows CounterStrikeSharp (+ MultiAddonManager if installed)
- `css_plugins list` → shows "GunGame CS2"

Connect from your client (`connect localhost`), and you should spawn with the
first weapon and see the GunGame HUD.

---

# Config reference (GunGameCS2.json)

| Key | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master switch |
| `WeaponOrder` | 35 levels (full arsenal) | Progression, CS2 short names (kill-feed names). Last entries: `taser`, `hegrenade`, `knife` |
| `KillsPerLevel` | `2` | Kills to advance a level |
| `KillsPerLevelOverride` | SMG/rifle 3, finishers 1 | Per-level override, e.g. `{ "24": 3 }` |
| `TurboMode` | `true` | New weapon immediately on level up |
| `KnifeSteal` | `true` | Knife kills steal a level |
| `KnifeStealMinLevel` | `2` | Victims at/below this level can't be stolen from |
| `KnifeStealOnGrenadeLevel` | `false` | Allow stealing while on the HE level |
| `ObjectiveBonus` | `0` | Levels for planting/defusing (never wins the game) |
| `SuicidePenalty` | `true` | Lose a level on suicide/world death |
| `TeamKillPenalty` | `false` | Lose a level on team kill |
| `MaxLevelsPerRound` | `0` | Level-up cap per round, 0 = unlimited |
| `HandicapMode` | `false` | Late joiners start at the average level |
| `BotsCanWin` | `false` | Bots park on the last level instead of winning |
| `BotAutoFill` | `true` | Keep bots on the server |
| `BotQuotaMode` | `"fill"` | `fill` = bots top up to a total (humans displace them); `normal` = constant bot count |
| `BotAutoFillSlots` | `0` | Total (fill) or bot count (normal), 0 = server max slots |
| `ReplenishGrenade` | `true` | Fresh HE after each throw on the grenade level |
| `GiveArmor` | `true` | Kevlar + helmet on spawn |
| `HudEnabled` | `true` | Center HUD with level/kills/leader |
| `WelcomeMessage` | `true` | Greet joiners with rules, commands and the deathcam-mute tip |
| `LeaderGlowEnabled` | `true` | Green model tint on the leader(s), line-of-sight only |
| `CountWarmupKills` | `false` | Count kills during warmup |
| `WinnerDelay` | `15` | Seconds of freeze + countdown (and map vote) before map change |
| `WinnerCommand` | `""` | Override the map change (e.g. `mp_restartgame 1`). Empty = use MapCycle. Disables the vote |
| `MapVoteEnabled` | `true` | Vote on the next map during the win freeze (type 1-N in chat) |
| `MapVoteOptions` | `3` | Number of maps in the vote (2-5) |
| `MapNames` | `{}` | Display names for cycle entries, e.g. `{ "3111189015": "gg_simpsons_dust" }` |
| `MapCycle` | stock maps | Win rotation: `de_dust2`, `ws:<mapname>`, or `<workshopID>` |
| `StartupWorkshopMap` | `""` | Workshop map to switch to shortly after boot |
| `SoundVolume` | `0.5` | 0.0–1.0, applies to file-based sounds and sound events |
| `LevelUpSound` | UI sound | File path (`sounds/...vsnd`) or sound event name |
| `LevelDownSound` | UI sound | Plays only for the player who lost the level |
| `KnifeStealSound` | UI sound | Plays for the stealer |
| `WinnerSound` | UI sound | Plays for everyone |
| `FinalLevelSound` | `""` | Plays for EVERYONE when someone reaches the final (knife) level |
| `PrecacheSoundEventFiles` | `[]` | Soundevent files to precache — required for sound events from extra addons (e.g. `["soundevents/soundevents_gungame.vsndevts"]`) |
| `AnnouncerMuteCommand` | `"qs"` | Command forwarded by `!ggsound` so one toggle also mutes the QuakeSounds announcer; `""` to disable |

Reload after config edits: `css_plugins reload GunGameCS2`.

---

# Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `meta list` → unknown command | Metamod line missing or too low in `gameinfo.gi` (CS2 updates wipe it) |
| Server hangs at boot, port never opens | `+host_workshop_map` on the command line — use `StartupWorkshopMap` instead |
| "Unable to establish connection" after a CS2 update | Server outdated — run the SteamCMD update command |
| Settings revert each map | `gamemode_casual_server.cfg` missing (step 4) |
| Round ends after ~2 min | Set `mp_roundtime_defuse`/`mp_roundtime_hostage`, not just `mp_roundtime` |
| No quake sounds | Client must reconnect once after MultiAddonManager is installed; check the plugin's `sounds` config isn't empty |
| Bots stand still on a workshop map | Map has no nav mesh — nothing you can do server-side |
| `Could not PreloadLibrary ...` spam at boot | Harmless, ignore |
