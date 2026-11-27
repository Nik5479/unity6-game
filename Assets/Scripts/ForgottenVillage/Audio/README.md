# Audio

Sound and music: the audio manager, stings, footsteps, knocks, and
ambience. The wiring notes live here too.

Eventually moves here: `AudioManager.cs`, `AUDIO_WIRING.md`.

Must NOT go here: gameplay triggers that *call* audio (monster knock
sequencing stays in `AI/`, game-over stings are triggered by
`GameManager.cs`), or voice/narration assets themselves.
