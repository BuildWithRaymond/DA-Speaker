# Contributing to DA Speaker

Bug reports, documentation improvements, and focused pull requests are welcome.

## Before you start

- Search existing issues before opening a new one.
- For a larger feature, open an issue describing the user problem first.
- Keep changes focused and preserve the application's plain-language interface.
- Treat other contributors with respect. Explain disagreements with examples.

## Development

You need Windows x64 and the .NET 10 SDK. See the
[development guide](docs/development.md) for build, test, and publish commands.

Automated tests use fake chat input and private test windows. They should never
send messages to a running game or alter the user's clipboard.

Changes to input or playback need tests that cover cancellation, cleanup, and
recovery. UI changes should include screenshots at a normal and compact window
size. Test display scaling when changing custom controls.

## Pull requests

Describe the problem, the change, and how you verified it. Include steps to
reproduce bugs. Mention any live-client checks separately from automated tests:
a successful Windows input call does not prove that chat appeared in game.

Do not commit generated builds, personal settings, chat logs, or credentials.
If you share a troubleshooting log, review it first: it can contain script text
and game-window information.

By contributing, you agree that your contribution may be distributed under this
repository's [MIT license](LICENSE). Preserve applicable third-party notices.
