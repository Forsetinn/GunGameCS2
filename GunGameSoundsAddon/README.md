# GunGame Sounds addon

Custom per-player sounds for the GunGame plugin: level up, level down (you got
knifed!), and a final-level theme that plays for everyone when someone reaches
the knife.

## Quick setup (drag and drop)

A ready-made sound addon is published on the workshop - no building required:

**Workshop ID: `3742790165`** (GunGame Sounds)

1. Install [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager)
   on your server (extract into `csgo/`).
2. Add the addon ID in `csgo/cfg/multiaddonmanager/multiaddonmanager.cfg`:
   ```
   mm_extra_addons "3742790165"
   ```
   (comma-separate if you mount more, e.g. the Quake announcer addon
   `3461824328`: `"3461824328,3742790165"`)
3. Point the GunGame plugin config at the sound events:
   ```json
   "LevelUpSound": "GunGame.LevelUp",
   "LevelDownSound": "GunGame.LevelDown",
   "FinalLevelSound": "GunGame.FinalLevelMusic",
   "PrecacheSoundEventFiles": ["soundevents/soundevents_gungame.vsndevts"]
   ```
4. Restart the server. Clients download the addon automatically on connect.

That's it. Players can mute the sounds individually with `!ggsound`.

## Building your own sounds instead

Want different audio? Build your own addon in ~30 minutes:

### 1. Install CS2 Workshop Tools

Steam -> Library -> Counter-Strike 2 -> Properties -> Installed Files /
DLC -> install **Counter-Strike 2 Workshop Tools**, then launch the tools.

### 2. Create the addon

In the Workshop Tools launcher: **New Addon** -> name it e.g. `my_gg_sounds`.
This creates `.../Counter-Strike Global Offensive/content/csgo_addons/my_gg_sounds/`.

### 3. Add your sound files

Copy your audio as **.wav (16-bit PCM, 44.1 kHz)** into:

```
content/csgo_addons/my_gg_sounds/sounds/gungame/
    levelup.wav       <- plays when you gain a level
    leveldown.wav     <- plays when you LOSE a level (e.g. got knifed)
    finallevel.wav    <- plays for everyone when someone reaches knife level
                         (keep it 10-20s, it plays once and can't be stopped)
```

### 4. Add the soundevents file

Copy `soundevents/soundevents_gungame.vsndevts` from this folder into:

```
content/csgo_addons/my_gg_sounds/soundevents/soundevents_gungame.vsndevts
```

Hard-won rules baked into that template - keep them if you edit it:
- Do NOT name the file `soundevents_addon.vsndevts`: every workshop map uses
  that filename and the map's copy shadows yours, silently.
- Keep the `<!-- kv3 ... -->` header line - without it the compile fails.
- Reference audio via `vsnd_files_track_01` - the `csgo_mega` event type
  ignores other property names and your events play silence.

### 5. Compile + publish

1. In the tools' **Asset Browser**, double-click each sound and the
   soundevents file so they compile (watch the console for errors).
2. **Tools -> Counter-Strike 2 Workshop Manager** -> New Submission ->
   visibility Public or Unlisted (the in-game publisher's "Hidden" can't be
   downloaded by servers!) -> Submit. New uploads sit in Steam review for
   ~30-90 minutes before servers can fetch them.
3. Use YOUR new workshop ID in `mm_extra_addons` and keep the same event
   names in the GunGame config.

### Tips

- A sound event can list multiple files for random variation:
  `vsnd_files_track_01` only takes one; duplicate the event block per sound
  and rotate via config, or keep one file per event (simplest).
- Updating audio later: replace the .wav, recompile, Re-Upload, wait for
  Steam review, restart the server (subscribed clients auto-update).
