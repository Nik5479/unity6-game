# Player

Hero Nilesh: movement, camera, animation, torch, and the `Player` singleton.

Eventually moves here: `PlayerController.cs`, `NileshAnimator.cs`,
`CharacterWalk.cs`, `CameraFollow.cs`, `TorchController.cs`.

Must NOT go here: game-over / survived state (that is `GameManager.cs`),
monster or NPC behaviour (see `AI/`), input UI widgets (see `UI/`).
