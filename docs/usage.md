# Using DASpeaker

## Choose your game window

Open Dark Ages, choose **Refresh**, and select the intended window. When multiple
windows have the same title, the selector includes their process IDs. Hover over
the selector for details. Keep local chat closed and empty before starting.

**Send test** sends `GLIOCA TEST`. Check the exact result in game before using a
longer script. The app keeps its selected target throughout a run and does not
switch focus. If the game restarts, Stop, Refresh, and select the new window.

## Prepare your script

Paste text into **Your script**, or choose **Open** to load a text file. **Save**
writes a separate text file. DASpeaker also remembers your current draft.

The preview shows exact messages, character counts, and pauses. Select a message
to locate its source text; hover over it to see its source line. At smaller window
sizes, switch between **Script** and **Preview**.

### Script rules

- Messages may contain up to **59 characters**, including spaces and punctuation.
- With automatic splitting enabled, adjacent lines form a paragraph. Whitespace
  between words becomes a single space, and whole words are packed into messages.
- With splitting disabled, each source line is one message. Lines longer than
  59 characters are rejected. Use spaces rather than tabs.
- Blank lines create a paragraph break. Empty messages are not sent.
- Use printable ASCII characters. Replace smart quotes, accented letters, and emoji.
- Put `[wait 5s]` or `[wait 5000]` on its own line for a five-second pause.
- Waits use nonnegative whole numbers, up to 24 hours per command.
- Explicit waits replace the automatic pause at that boundary. Consecutive waits
  add together. A zero wait is allowed.
- There is no automatic pause after the last message. An explicit final wait is honored.

Fix highlighted problems before starting. Clicking a problem takes you to its
location in the script.

## Control playback

| Control | What happens |
| --- | --- |
| Start speaking | Begin from the first message |
| Pause | Finish the current message, then freeze the remaining wait |
| Resume | Continue from the paused position |
| Stop | Finish current cleanup, cancel future messages, and reset the queue |

The script, target, and pace are locked during playback. The cue marker follows
the current step, and the bottom strip shows message progress and remaining wait.

## Pace and settings

| Setting | Default |
| --- | --- |
| Between messages | 3.5 seconds |
| Between paragraphs | 5 seconds, replacing the ordinary message delay |
| Wait for chat to open | 250 ms |
| Wait before sending | 150 ms |
| Gap between key events | 10 ms |

Advanced input timings live in **Settings**. Change them only when troubleshooting
delivery. Clipboard settling and the short wait after submission are handled by
the application.

Enable optional shortcuts in Settings:

- **F8:** Start / Resume
- **F9:** Pause
- **F10:** Stop

Shortcuts are off by default. Conflicts with other apps are reported.

## If speaking is interrupted

DASpeaker pauses on input errors. Check the game and clear or close any pending
chat before resuming. The failed message may already have appeared; retrying can
produce a duplicate. The app asks for acknowledgment before recovery.

Use **Settings → Troubleshooting → Save log** for an activity record. Logs may
include your script text and window details, so review them before sharing.

The app checks that Windows input calls complete; it cannot confirm game-side
delivery. Inspect the game if a message appears missing.

## Where your draft lives

Draft, timings, window position, wrapping, shortcut preference, and the remembered
window hint are stored in `%LocalAppData%\DASpeaker\settings.json`. Existing settings
from the former application folder are loaded automatically. The remembered
window must still match its identity before use.
