# Trap controls

- Q: LEGO preview; E: banana preview. Press the same key again or Esc to cancel.
- Left click: play the existing installation animation; its event installs the selected trap.
- KidTrapHUD.prefab shows model icons and key labels at the right center for the owning human kid only.
- Banana.zip / LEGO.zip: original archives, preserved. Extracted models, textures and URP materials are in their named folders.
- TrapBox.prefab / BananaTrap.prefab: local previews and offline objects.
- Multiplayer/NetworkTrapBox.prefab / NetworkBananaTrap.prefab: registered network objects.
- Both use the existing 0.3m placement offset; the model and collider base are aligned to ground. Longest footprint side: 0.6m.
- Server validates ownership, type (0 LEGO / 1 banana), range, ground hit and cooldown.
- Network protocol is now 5. All multiplayer participants need the updated build.
- This adds selection, visuals and installation; no new damage/slow effect is included.
