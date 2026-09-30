# Ceremonial desktop design

Date: 2026-09-28.

## Delivered

- Obsidian and charcoal surfaces, ivory text, and antique gold actions.
- Engraved geometric heading accent and a matching speech-mark seal.
- Georgia headings, quieter utility text, and monospaced cue numbers.
- A framed playback area with transport symbols and progress.
- Consistent game-picker and timing-input chrome with native input behavior.
- Compact script/preview layouts and matching Settings pages.
- Updated repository showcase images from actual application renders.

## Validation

- 69 existing tests passed: 68 in the sandbox, plus the atomic settings-save test
  outside the sandbox. Windows File.Replace is denied by the sandbox.
- A new assertion in the existing DPI tests reproduced a clipped checkbox label
  at both 150% and 200%. The custom checkbox now measures its own text and glyph
  space; both cases pass.
- Inspected ready, running, paused, empty, invalid, compact, Preferences, and
  Troubleshooting captures. Inspected simulated 150% and 200% DPI transitions.
- Main text on the editor surface: 14.66:1 contrast. Secondary text: 7.13:1.
  Dark text on the main gold action color: 9.94:1.
- No game messages were sent. Form tests use fake game input.

## Local artifacts

- Executable: `artifacts/premium/DASpeaker.exe`.
- Before: `artifacts/design/before-main.png`.
- After: `docs/images/screenshots/ceremonial-main.png`.
- State captures: `artifacts/design/`.

The executable is a Windows x64, framework-dependent single-file build and
requires the .NET 10 Desktop Runtime. Physical multi-monitor DPI transitions
and live game delivery were not exercised during this design pass.
