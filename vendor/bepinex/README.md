# bepinex (vendored)

This directory contains a bundled copy of the upstream mod loader. It is the install-time
source of truth: install.cmd extracts directly from here and never reaches out to the network.
Refresh manually with `pixi run update-deps`, then commit.

## Snapshot

- Asset: `BepInEx_win_x64_5.4.23.5.zip`
- Tag: `v5.4.23.5`
- Commit: `57f1fb859bd4d0264cd2a59074d0e96c6a492a33`
- Upstream URL: https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip
- SHA-256: `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`
- Fetched at: 2026-04-30T21:58:19.1334036+01:00
- Source: github

Do not edit this directory by hand. Run ``pixi run package`` (or CI release) to refresh.
