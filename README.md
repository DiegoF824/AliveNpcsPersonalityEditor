# AliveNpcs Personality Editor

An in-game editor for the NPC personality profiles used by [AliveNpcs](https://www.nexusmods.com/stardewvalley/mods/43475).

It is a standalone companion mod: install it alongside AliveNpcs, then press `F10` in a loaded save to write, save, or reset personality overrides without editing JSON by hand.

## Requirements

- Stardew Valley 1.6+
- SMAPI 4.0+
- AliveNpcs 1.4.3+

Generic Mod Config Menu is not required. AliveNpcs itself still needs its normal AI-provider configuration.

## Features

- Stardew-style menu with NPC portraits and current-profile previews.
- Configurable editor hotkey (`F10` by default).
- Tabs for vanilla NPCs, enabled SVE NPCs, and compatible custom NPCs.
- Immediate AliveNpcs reload after a save; the next AI conversation can use the new profile.
- Reset removes only the override and restores the AliveNpcs default profile.
- Localized UI: English, Brazilian Portuguese, Spanish, French, German, and Italian.
- Safe file replacement when saving personality data.

The editor follows the NPC availability returned by the current AliveNpcs API. It does not bypass AliveNpcs compatibility settings or the community AI opt-out dataset.

## Install

1. Install SMAPI and AliveNpcs.
2. Extract `AliveNpcsPersonalityEditor` into `Stardew Valley/Mods`.
3. Load a save and press `F10`.
4. Select a villager, write a profile, and click **Save**.

## Configuration

The first launch creates `config.json` in the mod folder:

```json
{
  "OpenEditorKey": "F10"
}
```

Change `F10` to any valid SMAPI button name.

## Data and multiplayer

Overrides live in `custom_personalities.json` in the editor's own mod folder. The file is global to that game installation, so it applies to every save on that computer. It can be backed up or copied to another installation.

In multiplayer, players who want the same personality profiles should use the same data file. The editor does not change vanilla dialogue, schedules, portraits, heart events, friendship values, or existing AliveNpcs memories.

## Writing profiles

Describe a character's temperament, values, speech style, habits, boundaries, and how they react to trust or conflict. Keep a recognizable core and let AliveNpcs add the save-specific memories, relationships, mood, gossip, and story context.

Example:

> Sebastian is private and dryly funny, using sarcasm when he feels cornered. He values independence and notices when someone respects his space. Around trusted friends he becomes unexpectedly playful and talks more openly about technology, music, and leaving town one day.

## Support

- [AliveNpcs Discord](https://discord.gg/8vUfXEH852)
- [Buy Me a Coffee](https://buymeacoffee.com/gaticadev)
- [LivePix](https://livepix.gg/gaticadev)
