# Input design

DASpeaker targets the selected Dark Ages window and validates its handle,
process ID, and `Darkages` window class throughout dispatch. It does not change
the selected target during playback.

Each message captures supported clipboard formats, places the message text on
the clipboard, opens local chat, pastes, submits, and restores the clipboard.
Enter uses `PostMessageW`; Ctrl/V use ordered `SendMessageTimeoutW` calls with
a one-second timeout. The app waits 100 ms after submitting before clipboard
restoration. It releases tracked keys and restores clipboard content on failure.

Pause and stop take effect between messages so an in-progress message can finish
its cleanup. An interrupted message may already have appeared in game, so recovery
requires an explicit acknowledgment before retrying.

## References and acknowledgments

Special thanks to **[ewrogers](https://github.com/ewrogers)** for
**[SleepHunter](https://github.com/ewrogers/SleepHunter4)**. Its input implementation
was a reference for keyboard-message construction, scan-code validation, and
window ownership checks. See [credits](../CREDITS.md) and
[third-party notices](../THIRD_PARTY_NOTICES.md).

- [SleepHunter input message construction](https://github.com/ewrogers/SleepHunter4/blob/main/src/SleepHunter.Interop/Input/ClientInputMessages.cs)
- [SleepHunter Windows input](https://github.com/ewrogers/SleepHunter4/blob/main/src/SleepHunter.Interop/Input/WindowsClientInput.cs)
- [PostMessageW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew)
- [SendMessageTimeoutW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw)

## What tests establish

Automated tests check message construction, ordering, delays, cancellation,
window validation, and cleanup. They use fake input and private test windows.
They do not establish that every message reached game chat. The game provides
no receipt signal to this application; live validation remains separate.
