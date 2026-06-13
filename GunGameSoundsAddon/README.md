# GunGame custom sounds addon - build guide

Build your own workshop sound addon (e.g. Mario-style level sounds) in ~30 min.
The GunGame plugin is already wired to use it - you only need to publish the
addon and point the config at the sound events.

## 1. Install CS2 Workshop Tools

Steam -> Library -> Counter-Strike 2 -> Properties -> Installed Files /
DLC -> install **Counter-Strike 2 Workshop Tools**. Launch CS2 with the
`-tools` launch option (or pick "Launch Workshop Tools" from the play dialog).

## 2. Create the addon

In the Workshop Tools launcher: **New Addon** -> name it `gungame_sounds`.
This creates `.../Counter-Strike Global Offensive/content/csgo_addons/gungame_sounds/`.

## 3. Add your sound files

Copy your audio as **.wav (16-bit PCM, 44.1 kHz, mono or stereo)** into:

```
content/csgo_addons/gungame_sounds/sounds/gungame/
    levelup.wav       <- plays when you gain a level
    leveldown.wav     <- plays when you LOSE a level (e.g. got knifed)
    finallevel.wav    <- plays for everyone when someone reaches knife level
                         (e.g. Mario theme - keep it 10-20s, it plays once)
```

(Want more, e.g. knife-steal or winner sounds? Add the .wav and a matching
event block in the .vsndevts - the GunGame config can point any of its sound
entries at any event name.)

## 4. Add the soundevents file

Copy the `soundevents/soundevents_gungame.vsndevts` from this folder into:

```
content/csgo_addons/gungame_sounds/soundevents/soundevents_gungame.vsndevts
```

IMPORTANT: do NOT name it `soundevents_addon.vsndevts` - every workshop map
uses that filename, so the map's file shadows yours and your events never
load. Use a unique name and list it in the GunGame plugin config:

```json
"PrecacheSoundEventFiles": ["soundevents/soundevents_gungame.vsndevts"]
```

## 5. Compile + publish

1. With the addon open in Workshop Tools, open the **Asset Browser** - it
   compiles the .wav files to .vsnd automatically (full recompile: Tools ->
   Recompile All Files, or just touch each asset).
2. Open **Workshop Manager** (in the tools), create a new submission for the
   addon, set visibility to **Unlisted** (safest for copyrighted audio) and
   publish. Note the workshop ID from the URL.

## 6. Hook it up on the server

1. Add the new ID to `csgo/cfg/multiaddonmanager/multiaddonmanager.cfg`:
   ```
   mm_extra_addons "3461824328,YOUR_NEW_ID"
   ```
2. Point the GunGame config (`configs/plugins/GunGameCS2/GunGameCS2.json`) at
   the events:
   ```json
   "LevelUpSound": "GunGame.LevelUp",
   "LevelDownSound": "GunGame.LevelDown",
   "FinalLevelSound": "GunGame.FinalLevelMusic",
   ```
3. Restart the server (MultiAddonManager reads its config at boot).

All these sounds are per-player (client-sided) already: LevelDown plays only
for the victim, LevelUp/KnifeSteal only for the actor, Winner for everyone.

## Notes

- Sound not playing? Check the addon mounted (server console shows
  MultiAddonManager downloading it) and that the event name in the config
  exactly matches the .vsndevts entry.
- Want random variation? A sound event can list multiple files:
  `sounds = ["sounds/gungame/levelup1.vsnd", "sounds/gungame/levelup2.vsnd"]`
  and one is picked at random each time.
- Keep clips short (under ~3s) - they overlap with the Quake announcer.
