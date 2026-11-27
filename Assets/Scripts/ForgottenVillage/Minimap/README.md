# Minimap

The village minimap: rendering, player/monster markers, and fog of war
(if added). No existing script covers this yet — all code here is new.

Must NOT go here: pathfinding (`VillagePathfinder.cs` in `AI/`),
village construction (`VillageBuilder.cs` in `Village/`), or HUD
controls (see `UI/`). The minimap only *reads* positions; it never
moves anything.
