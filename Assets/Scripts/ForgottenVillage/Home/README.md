# Home

Nilesh's house and the core horror loop: the door, its lock, and
knock / entry resolution. The rule "night knock in a mimicked voice —
keep the door LOCKED" lives here.

Eventually moves here: `DoorController.cs`.

Must NOT go here: the monster's walk/chase/knock sequencing
(`MonsterKnock.cs` in `AI/`), or game-over / survived state
(`GameManager.cs` — it calls into the door, it does not live here).
