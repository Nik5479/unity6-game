# UI

On-screen controls and HUD: virtual joystick, buttons, icons, subtitles,
objectives, and safe-area fitting. Symbol-only controls, no text labels
on action buttons (project rule).

Eventually moves here: `MobileControls.cs`, `VirtualJoystick.cs`,
`ControlIcons.cs`, `SafeAreaFitter.cs`.

Must NOT go here: gameplay logic behind the buttons (player movement
stays in `Player/`, door interaction in `Home/`), or save data (see `Save/`).
