# Borderless Everything
This application allows any program to run as a borderless window. The default fullscreen mode featured in many games does not allow other windows to be placed above it and does not allow users to mouse away from it without minimizing the game.

## Join the BorderlessMinecraft Discord: https://discord.gg/gbR7qba6uv

Basic usage is simple. All running programs will be listed in the list of open programs. Selecting one and clicking "Go Borderless" will remove all window decoration and set its size to match that of your computer's main monitor, making it go in Borderless Fullscreen

Clicking "Restore Window" will return it to a normal size and show the menu bar.

Clicking "Set Title" is for usage with multiple accounts, and will append "(Second Account)" to the existing title. Optionally, a custom text can be used instead and will be appended to the current game title. This is primarily useful when using OBS to record multiple instances of the same game by locking each game capture to a specific window title.

Alteratively, you can click "Advanced" and enter a size and position manually.

## New features in 1.3.0

Settings are now saved when the app is closed.

The settings menu has six options:

- Start on boot: pretty self explanatory. :)
- Start minimized: When start on boot is enabled, Borderless Minecraft will launch minimized.
- Minimize to tray: Clicking minimized will hide Borderless Minecraft in the tray and remove it from the taskbar. You can click its tray icon to bring it back.
- Automatic borderless: Minecraft will go borderless autoamtically when launched. As of 1.3.0, this only works for for vanilla and any java window with "Minecraft" in the title.
- Preserve Taskbar: When going borderless, the taskbar will remain visible.

### Important note on automatic borderless:

Administrator privilages are required for automatic borderless as the app monitors when processes start and stop using Windows Management Instrumentation (WMI).
