# Save

Persistence: save/load of progress, settings, and unlocks. No existing
script covers this yet — all code here is new.

Must NOT go here: runtime game state (`GameManager.cs` stays at the
root until a Core folder is agreed), player prefs abused as a save
system, or anything that writes outside the game's own save location.
New code must keep working when no save file exists yet.
