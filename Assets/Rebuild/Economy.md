# Campaign gold

The current run has one CampaignWallet owned by BattleSceneFlow. It survives
Scene changes, preparation and failed retries. BeginRun creates a fresh wallet.
Currency is not saved to disk between app sessions.

Assets/Rebuild/Resources/RebuildEconomy.asset controls Starting Gold (default 0)
and the Level Rewards list. Each entry has a unique Level number, Gold amount,
and Units list (Role and Count per entry). Defaults: Level1 = 10 gold + one Tank;
Level2 = 100 gold, no units. Missing levels have no reward. Configuration is loaded
at run creation; edit it outside Play Mode and start a new run to apply changes.
Only victory awards gold, once per level per run, including the final level.
No tower kill, damage or defeat gold is awarded. Runtime never mutates the asset.

Unit rewards are queued together with the once-per-level gold award. On entering
UpgradeScene, they are added to this run's preparation inventory with new unique
member IDs, without changing the roster asset or auto-deploying units. Reopening
UpgradeScene does not duplicate rewards. The warehouse remains 2x6 and deployment
remains capped at six. If storage is full, excess units remain queued for the run
and a hint reports them; no member is overwritten. Claiming/replacing from this
overflow queue has no UI yet. New games clear both received and pending units.

LegacyUpgradeScene alone displays GOLD balance, anchored at top-right with
28 horizontal / 20 vertical reference-pixel margins. It updates after spending.
Combat HUD and title screens do not display gold. The settlement card now displays
the actual gold/unit rewards granted by this settlement (not the wallet balance).
The snapshot clears on defeat and new runs. Direct settlement Scene Play previews
the configured Level1 reward without granting anything.
Existing scene UI and camera transforms are unchanged.

Shop controllers should validate the selected product and destination, then call
BattleSceneFlow.TrySpendGold(price). It returns false for insufficient funds,
Scene transitions, or any Scene other than UpgradeScene. Nonpositive prices are
configuration errors and throw ArgumentOutOfRangeException. This change adds no
products or prices and does not charge for existing warehouse deployment.

Offline compilation and wallet rules checks cover duplicate rewards, accumulation,
insufficient/exact payment, invalid values, integer overflow and new-run isolation.
PlayMode tests additionally cover settlement -> upgrade -> Level2, UI updates,
spending restrictions, defeat and new-run reset. Unity UI / PlayMode execution
is not performed automatically at the user's request.
