# Upcoming map preview

UpgradeScene adds Preview Next Level between its level/lives text and the authored
Start Wave button. It is 85% of Start Wave's rendered width and 75% of its height;
only the generated level label moves upward to make room. The authored Start Wave
transform, camera, warehouse and formation positions remain unchanged.

The fullscreen root Canvas displays the next battle's actual saved Scene via an
additive load and a RenderTexture. A temporary camera fits static renderer bounds,
including decorative borders, using the map camera's viewing angle. Rendering is
isolated on temporary layer 31. No copied layout asset or second editable map is
stored. Temporary Scene layers/cameras are never saved. X at top-right closes.

BattleSceneFlow registers the path before loading: flow listeners ignore the
preview Scene; BattleSession cannot auto-start or manually start simulation there;
HUD, presentation and tutorial initialization are suppressed. The preview then
disables all loaded scripts, source cameras, UI, audio, colliders and lights, and
uses its own camera/light. UpgradeScene stays active. No combat state, lives, gold,
card purchases or deployment are modified. Start Wave and Claim Card are blocked
while previewing. Closing waits for load completion if necessary, unloads the
temporary Scene, releases its RenderTexture and allows normal battle entry again.

The source Scene must be saved and listed in Build Settings. Current targets are
Level1GridBattle or Level2GridBattle according to NextBattleLevelNumber. PlayMode
coverage checks actual Scene data, zero simulation, root Canvas, unchanged run
state, close/unload, early close/reopen and normal battle after preview. Source
compilation/metadata checks run offline; Unity visual and lifecycle tests are not
executed automatically per the user's instruction.
