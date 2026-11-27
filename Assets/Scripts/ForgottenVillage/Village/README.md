# Village

The world itself: procedural village construction, model loading, and
world pickups scattered around Bhairavpur.

Eventually moves here: `VillageBuilder.cs`, `GltfModelLoader.cs`,
`Pickup.cs`.

Must NOT go here: AI/pathfinding (see `AI/`), the player's house and
door-lock horror loop (see `Home/`), or day/night and sky (see `Weather/`).
