# Attempts per Level

Each full squad defeat counts as one failed attempt. The first successful goal arrival adds one successful attempt. Counts are independent for displayed levels 1, 2 and 3 (scene names and currentLevel do not match these numbers).

Tutorial checkpoint restoration starts a fresh attempt after the defeat has been recorded. Checkpoints and purchased lives cannot reset the counters because analytics is separate from OffenseData. Unresolved attempts are excluded. Starting a new game creates a fresh run while retaining earlier runs in the JSON archive.

The dedicated AttemptResults scene opens when leaving EndGame using Play Again, or leaving the out-of-lives screen using Exit & Restart Game. Back to Start resets gameplay data and loads StartGame. Its bars show this run's resolved attempts, not an average. Completed levels include all defeats plus the successful attempt. Incomplete levels include all defeats, including the final defeat before exiting. Only unreached levels show zero; completion status is shown separately. Do not include incomplete levels in average attempts to completion.

Data is saved after starting a run and after each resolved attempt, at Application.persistentDataPath/attempts-per-level.json. Each run has an ID and each level stores failures and completion status. Attempts = failures + (completed ? 1 : 0). Collection: AttemptAnalytics.cs. Visualization: AttemptResultsScreen.cs.

## Local verification

Run Unity in batch mode with `-executeMethod AttemptAnalyticsVerification.Verify`. This checks independent levels, duplicate callbacks, purchased lives, checkpoint restoration, unresolved attempts, JSON saving and fresh runs. It restores the existing archive after verification.

## Playtest checklist

- Start from StartGame, lose once, then retry the tutorial: the defeat must remain counted.
- Lose all three lives; purchase a retry for 10 gold if affordable. Lose again: the eventual chart must include all four defeats.
- Complete level 1 after two defeats: level 1 shows 3 Completed; level 2 starts with its own 3 lives.
- Exit from the out-of-lives screen: view the chart before continuing to StartGame.
- Finish the final level: the original ending screen appears; use Play Again to view all three bars, then Back to Start.
- Restart: the new run has fresh counts; previous runs remain in the JSON file.

WebGL browser persistence across reloads has not been validated in this local desktop playtest.
