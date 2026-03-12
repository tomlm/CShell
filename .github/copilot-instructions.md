# Copilot Instructions

## General Guidelines
- Global helper methods should always delegate to `_shell` methods rather than calling framework APIs directly.
- Use `print()` as an alias for `Console.Out.WriteLine()` and `error()` as an alias for `Console.Error.WriteLine()` on `CShell`.

## Code Style
- Follow specific formatting rules.
- Adhere to naming conventions.