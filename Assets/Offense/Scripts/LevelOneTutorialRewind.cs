using System;
using System.Collections.Generic;
using ReverseTD.Defense;
using ReverseTD.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReverseTD.Offense
{
    // One in-memory checkpoint, replaced whenever a tutorial banner appears.
    // Retaining combat objects preserves projectile callbacks and target/formation references.
    public sealed class LevelOneTutorialRewind : MonoBehaviour
    {
        private OffenseWaveManager owner;
        private OffenseData data;
        private string entryData;
        private Action restoreCheckpoint;
        private readonly HashSet<GameObject> retired = new HashSet<GameObject>();

        internal void Initialize(OffenseWaveManager manager, OffenseData offenseData)
        {
            owner = manager;
            data = offenseData;
            entryData = data != null ? JsonUtility.ToJson(data) : null;
        }

        public static void Retire(GameObject obj)
        {
            var rewind = OffenseWaveManager.Instance != null ? OffenseWaveManager.Instance.TutorialRewind : null;
            if (rewind != null && obj.scene == rewind.gameObject.scene)
            {
                rewind.retired.Add(obj);
                obj.SetActive(false);
            }
            else Destroy(obj);
        }

        internal void Capture(Action replayHint)
        {
            if (owner.IsDefeated) return;
            // Expired shots from older checkpoints are no longer needed.
            retired.RemoveWhere(obj =>
            {
                if (obj == null) return true;
                if (obj.GetComponent<Projectile>() == null) return false;
                Destroy(obj);
                return true;
            });

            string savedData = data != null ? JsonUtility.ToJson(data) : null;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            var restorers = new List<Action> { owner.CaptureTutorialState() };
            foreach (OffenseSoldier unit in owner.GetComponentsInChildren<OffenseSoldier>(true))
                restorers.Add(unit.CaptureTutorialState());
            BoardView board = FindAnyObjectByType<BoardView>();
            if (board != null)
            {
                foreach (TowerHealth health in board.GetComponentsInChildren<TowerHealth>(true))
                    restorers.Add(health.CaptureTutorialState());
                foreach (TowerWeapon weapon in board.GetComponentsInChildren<TowerWeapon>(true))
                    restorers.Add(weapon.CaptureTutorialState());
            }
            var savedShots = new HashSet<Projectile>();
            foreach (Projectile shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (shot.gameObject.scene != gameObject.scene) continue;
                savedShots.Add(shot);
                restorers.Add(shot.CaptureTutorialState());
            }
            // Persistent wrecks are cosmetic; discard only wrecks created after this checkpoint.
            var savedWrecks = new HashSet<Transform>();
            if (board != null)
                foreach (Transform child in board.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Rubble") savedWrecks.Add(child);

            restoreCheckpoint = () =>
            {
                OffenseWaveManager.IsTutorialPaused = true;
                Time.timeScale = 0f;
                foreach (Projectile shot in FindObjectsByType<Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (shot.gameObject.scene == gameObject.scene && !savedShots.Contains(shot))
                    {
                        shot.gameObject.SetActive(false);
                        Destroy(shot.gameObject);
                    }
                foreach (EffectSprite effect in FindObjectsByType<EffectSprite>(FindObjectsSortMode.None))
                    if (effect.gameObject.scene == gameObject.scene)
                    {
                        effect.gameObject.SetActive(false);
                        Destroy(effect.gameObject);
                    }
                if (board != null)
                    foreach (Transform child in board.GetComponentsInChildren<Transform>(true))
                        if (child.name == "Rubble" && !savedWrecks.Contains(child))
                        {
                            child.gameObject.SetActive(false);
                            Destroy(child.gameObject);
                        }
                if (data != null && savedData != null) JsonUtility.FromJsonOverwrite(savedData, data);
                foreach (Action restore in restorers) restore();
                foreach (Projectile shot in savedShots) retired.Remove(shot.gameObject);
                if (board != null) board.RestoreLivingTowers();
                UnityEngine.Random.state = randomState;
                replayHint();
            };
        }

        internal void ShowDefeat()
        {
            HowToPlay.ShowLevelOneDefeat(Restart, Restore, restoreCheckpoint != null);
        }

        private void Restore()
        {
            // A replayed banner replaces the checkpoint, so invoke a stable local copy.
            Action restore = restoreCheckpoint;
            restore?.Invoke();
            ReverseTD.Analytics.AttemptAnalytics.BeginAttempt(1);
        }

        private void Restart()
        {
            if (data != null && entryData != null) JsonUtility.FromJsonOverwrite(entryData, data);
            HowToPlay.ResetLevelOneTutorial();
            SceneManager.LoadScene("DefenseSandbox");
        }
    }
}
