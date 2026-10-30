# Claim Card migration

Read-only source: Team5/Assets/UpgradeUI/Scripts/SquadScreen.cs (BuildQuota,
ClaimCard), AugmentOverlay.cs; Offense/Scripts/SquadRules.cs, OffenseData.cs,
AugmentCard.cs. No old runtime, coordinate system or shared asset was imported.

The quota bar and Claim Card button are below the 2x6 warehouse. Prices are the
actual old quotas: 10, 100, 500 gold, three purchases per run. Claim opens up to
three fixed choices: Add Attack, Add Tank and Add Heal. Choosing one spends its price. Cancel costs nothing and
retains the offer. Funds and card validity are checked again on purchase. All
cards claimed / insufficient funds disables the button. Start Wave is blocked
while the root-canvas card overlay is open. Existing UI positions are unchanged.

Per the user's explicit choice, GoldBonus and all stat upgrades are excluded. A unit card adds an
Attack, Tank or Heal to storage with a new stable ID, not directly to formation.
All three roles remain available even when already owned. Full storage disables
Claim Card, with no charge. Existing six-unit deployment and twelve-unit storage
limits remain. Every purchased unit uses the current base role definition; no
existing member, stat, ID or formation position is changed. New units remain in
storage until deployed. New runs reset cards and inventory; failed retries retain
them. No roster/config assets mutate.

Offline checks cover quotas, offer caching, duplicate/invalid purchases, full
storage, fixed three-role choice, stable IDs and unchanged existing members.
PlayMode coverage adds the button, root overlay, cancel/reopen, payment
and warehouse update. Unity UI / PlayMode execution is not performed automatically.
