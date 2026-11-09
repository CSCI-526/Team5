using System;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Analytics
{
    // Follows scene loads, battle sessions and run settlement. One tracker per attempt.
    public static class BattleAnalytics
    {
        private static BattleSession session;
        private static BattleAnalyticsTracker tracker;
        private static int handledScene;
        private static string currentScene = "";
        private static float sceneEnteredAt, runStartedAt;
        private static int runsInSession, runAttempts, runDefeats;

        public static BattleAnalyticsTracker Current => tracker != null && !tracker.IsFinished ? tracker : null;
        public static double SimSeconds => Current != null ? Current.Simulation.ElapsedTime : 0;
        public static float ProgressPct => Current != null ? Current.CurrentProgressPct : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Uninstall();
            session = null;
            tracker = null;
            handledScene = 0;
            currentScene = "";
            sceneEnteredAt = runStartedAt = 0;
            runsInSession = runAttempts = runDefeats = 0;
        }

        internal static void Install()
        {
            Uninstall();
            SceneManager.sceneLoaded += OnSceneLoaded;
            BattleSceneFlow.RunStarted += OnRunStarted;
            BattleSceneFlow.LevelSettled += OnLevelSettled;
        }

        private static void Uninstall()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BattleSceneFlow.RunStarted -= OnRunStarted;
            BattleSceneFlow.LevelSettled -= OnLevelSettled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindInitialScene()
        {
            if (GameAnalytics.IsActive) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        // Members whose slot changes when the editor's source slots are applied.
        public static int MovedMembers(BattleSimulation simulation, IReadOnlyList<int> sourceSlots)
        {
            try
            {
                var moved = 0;
                for (var destination = 0; sourceSlots != null && destination < sourceSlots.Count; destination++)
                    if (sourceSlots[destination] != destination && simulation.GetMemberAtSlot(sourceSlots[destination]) != null) moved++;
                return moved;
            }
            catch (Exception error) { GameAnalytics.Fail(error); return 0; }
        }

        internal static void Observe(AnalyticsEvent value)
        {
            var current = Current;
            if (current == null) return;
            switch (value.Name)
            {
                case "formation_edit": if ((value["result"] as string) == "applied") current.formationEdits++; break;
                case "pause_toggle": if (value["paused"] is bool paused && paused) current.pauses++; break;
                case "speed_change": current.speedChanges++; break;
                case "unit_inspect": current.inspections++; break;
            }
        }

        internal static void LateTick()
        {
            try
            {
                var current = Current;
                if (current == null) return;
                if (session != null) current.Sample(session.PlaybackSpeed);
                // Test runs and skipped settlements never raise LevelSettled.
                if (current.Simulation.Outcome != BattleOutcome.Running) FinishCurrent(0, false);
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || BattleSceneFlow.IsPreviewScene(scene) || scene.handle == handledScene) return;
            handledScene = scene.handle;
            try
            {
                CloseCurrent();
                PrepAnalytics.Leave();
                TutorialAnalytics.Reset();
                Unbind();
                var previous = currentScene;
                var previousSeconds = Time.realtimeSinceStartup - sceneEnteredAt;
                currentScene = scene.name;
                sceneEnteredAt = Time.realtimeSinceStartup;
                GameAnalytics.SetScene(scene.name);
                foreach (var root in scene.GetRootGameObjects())
                {
                    session = root.GetComponentInChildren<BattleSession>();
                    if (session != null) break;
                }
                // The flow may restore the session's failed attempts after this handler, so use the flow's count here.
                var run = BattleSceneFlow.IsRunActive;
                GameAnalytics.SetLevelContext(session != null ? session.DisplayedLevelNumber : run ? BattleSceneFlow.NextBattleLevelNumber : 0,
                    run ? BattleSession.DefaultLivesPerLevel - BattleSceneFlow.CurrentLevelLivesRemaining + 1 : session != null ? 1 : 0);
                GameAnalytics.Track("scene_enter", ("previous_scene", previous), ("previous_scene_s", previous.Length > 0 ? previousSeconds : 0));
                if (session == null) return;
                session.SimulationCreated += OnSimulationCreated;
                if (session.Simulation != null) OnSimulationCreated(session.Simulation);
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private static void OnSimulationCreated(BattleSimulation next)
        {
            try
            {
                if (next == null || session == null || (tracker != null && tracker.Simulation == next)) return;
                CloseCurrent();
                // StartBattle always builds a fresh simulation; only a tutorial rewind restores a clock past zero.
                var resumed = next.ElapsedTime > 0;
                var attempt = NextAttempt(session);
                GameAnalytics.SetLevelContext(session.DisplayedLevelNumber, attempt);
                if (resumed) GameAnalytics.Track("tutorial_rewind", ("checkpoint_sim_s", next.ElapsedTime), ("lives_left", session.AttemptsRemaining));
                runAttempts++;
                tracker = new BattleAnalyticsTracker(next, RouteLength(session), attempt, resumed);
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private static void OnRunStarted()
        {
            try
            {
                GameAnalytics.BeginRun();
                runsInSession++;
                runAttempts = runDefeats = 0;
                runStartedAt = Time.realtimeSinceStartup;
                GameAnalytics.Track("run_start", ("run_number_in_session", runsInSession));
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        // Raised inside Settle, so rewards, lives and exhaustion are already final.
        private static void OnLevelSettled(BattleOutcome outcome, int level)
        {
            try
            {
                var current = Current;
                if (current != null && current.Simulation.Outcome != BattleOutcome.Running)
                {
                    if (session != null) current.Sample(session.PlaybackSpeed);
                    FinishCurrent(BattleSceneFlow.LastRewardGold, BattleSceneFlow.IsOutOfLives);
                }
                if (outcome == BattleOutcome.Defeat) runDefeats++;
                if (outcome == BattleOutcome.Defeat && BattleSceneFlow.IsOutOfLives)
                    GameAnalytics.Track("game_over", ("run_real_s", RunSeconds), ("total_attempts", runAttempts), ("total_defeats", runDefeats));
                // Settle leaves the next level unchanged only when the campaign is over.
                if (outcome == BattleOutcome.Victory && BattleSceneFlow.NextBattleLevelNumber == level)
                    GameAnalytics.Track("campaign_complete", ("run_real_s", RunSeconds), ("total_attempts", runAttempts), ("total_defeats", runDefeats));
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private static float RunSeconds => Time.realtimeSinceStartup - runStartedAt;

        private static int NextAttempt(BattleSession value) => value.AttemptsPerLevel - value.AttemptsRemaining + 1;

        private static void FinishCurrent(int goldReward, bool gameOver)
        {
            var current = Current;
            if (current == null) return;
            current.Finish(goldReward, session != null ? session.AttemptsRemaining : -1, gameOver);
        }

        private static void CloseCurrent()
        {
            var current = Current;
            tracker = null;
            if (current == null) return;
            if (session != null) current.Sample(session.PlaybackSpeed);
            if (current.Simulation.Outcome == BattleOutcome.Running) current.Abandon();
            else current.Finish(0, session != null ? session.AttemptsRemaining : -1, BattleSceneFlow.IsOutOfLives);
        }

        private static void Unbind()
        {
            if (session != null) session.SimulationCreated -= OnSimulationCreated;
            session = null;
        }

        private static float RouteLength(BattleSession value)
        {
            if (value.Route == null) return 0;
            var points = value.Route.CaptureTopCenters();
            var length = 0f;
            for (var i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }
    }
}
