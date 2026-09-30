# Credits

## Thank you, ewrogers

Special thanks to **[ewrogers / Erik Rogers](https://github.com/ewrogers)** for
**[SleepHunter](https://github.com/ewrogers/SleepHunter4)** and for making that
work available to the Dark Ages community.

We studied SleepHunter's Windows input handling while developing DASpeaker.
Its keyboard-message construction and window-validation approach provided a
valuable reference for this project's input layer. Its distinction between
sending input and confirming a result in the game also informed our testing.

DASpeaker has its own script parser, playback controller, clipboard handling,
and interface. SleepHunter is not a runtime dependency. The upstream MIT notice
is preserved in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Design inspiration

**[Aosda](https://darkagesbot.com)** inspired the dark desktop presentation:
charcoal panels, restrained gold accents, and compact controls. The app also
includes this credit in Settings.

## Built with

- [.NET and Windows Forms](https://github.com/dotnet/winforms)
- [xUnit.net](https://github.com/xunit/xunit) for automated tests
- [Pillow](https://python-pillow.org/) for the optional icon-generation tool

The application icon and showcase layouts were created for DASpeaker. Showcase
images use the application's real interface with a sample script and fake input;
they contain no live game session or account data.
