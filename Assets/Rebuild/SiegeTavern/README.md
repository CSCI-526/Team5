# Siege Tavern prototype

Independent reverse tower defense + tavern preparation prototype using existing Team5 art and the existing 3x3 squad simulation. Existing campaign scenes, camera settings, economy and campaign progression are not connected to this mode.

## Play

Open `Assets/Rebuild/Scenes/SiegeTavernUpgrade.unity`, then press Play.

- 60 seconds to recruit, refresh, freeze offers, upgrade the tavern, train, sell and change formation.
- Recruit: 3 gold. Refresh: 1 gold. Tavern upgrade: 5 gold (tiers 1–3). Train: 3 gold (levels 1–3). Sell: +1 gold.
- Four offers; twelve owned followers; six deployed on nine formation slots. Three free starter followers.
- Select an owned follower, then click a formation slot to place or swap. Select a deployed follower and use Bench to reserve it. At least one follower must remain deployed.
- Begin Assault starts early; the preparation timer also starts the assault automatically.
- Existing Catgirl role visuals represent eight recruit types. Read the recruit cards for deployment bonuses. Bonuses are computed once at deployment.
- Followers move and fight automatically along a fixed three-cell-wide route. Pause/Resume and Retreat are available.
- Breach five defenses to win. Three failed assaults end the run. Surviving and defeated followers recover between assaults; progression and upgrades persist in memory during the run.
- No save/load, triple merging or live formation editing in this first prototype.

## Enemy construction

Every assault starts with empty tower sites, rather than prebuilt active towers. The enemy gathers 2 gold per simulation second. It spends 8 gold per tower, builds all available sites first, then upgrades the lowest-level surviving tower first. Upgrade costs are 10 and 14 gold; maximum level is 3. There are two sites in assault 1, three in assault 2, and four thereafter.

| Tower level | Health | Damage | Attack interval |
| --- | --- | --- | --- |
| 1 | 18 | 0.55 | 1.2 s |
| 2 | 28 | 0.85 | 1.05 s |
| 3 | 38 | 1.15 | 0.9 s |

Upgrades preserve the remaining HP percentage. Destroyed towers do not rebuild during the same assault. Enemy income and construction pause with simulation time. Tower models appear on completion and grow slightly taller on upgrade; labels display level and health.

The tower locations are authored safe building sites outside the route; the enemy does not dynamically block or rewrite paths. Opening the battle scene directly starts a valid starter team for quick testing.

## Files and builds

- Scenes: `SiegeTavernUpgrade.unity`, `SiegeTavernBattle.unity` under `Assets/Rebuild/Scenes`.
- Economy: `Scripts/Presentation/Minimal/SiegeTavernRun.cs`.
- Scene flow/UI: `Scripts/Presentation/Minimal/SiegeTavernMode.cs`.
- Enemy economy: `Scripts/Presentation/Minimal/SiegeEnemyBuilder.cs`.
- Opt-in simulation construction: `Scripts/Simulation/EnemyTowerConstruction.cs`.
- Scene generator: `Editor/SiegeTavernBuilder.cs` (refuses to overwrite existing scenes).
- Tests: `Tests/EditMode/SiegeTavernTests.cs`, `Tests/PlayMode/SiegeTavernPlayModeTests.cs`.
- Local verification logs/screenshots: `Logs/SiegeTavern`.

The Editor can switch between these scenes without changing the campaign build list. For a standalone/Web build of this prototype, make a separate Build Profile containing both scenes, with SiegeTavernUpgrade first. Web deployment has not been validated by this prototype's Editor tests.
