using System;
using System.Collections;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation
{
    // Opt-in packaged-player regression check; never runs during a normal play session.
    public sealed class TowerPlayerSmoke : MonoBehaviour
    {
        private bool errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-tower-smoke")) return;
            var host = new GameObject("Packaged tower regression check");
            DontDestroyOnLoad(host);
            host.AddComponent<TowerPlayerSmoke>();
        }
        private void Check(bool condition, string label)
        {
            if (!condition) { errors = true; Debug.LogError("SMOKE FAIL: " + label); }
            else Debug.Log("SMOKE PASS: " + label);
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) errors = true;
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            var fx = Resources.Load<Material>("TowerEffect");
            Check(fx != null && fx.shader != null && fx.shader.isSupported, "effect shader retained in player");
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.LevelSixScene);
            for (var i = 0; i < 5; i++) yield return null;
            var session = FindFirstObjectByType<BattleSession>();
            Check(session != null && session.Simulation != null, "level 6 starts");
            if (session == null || session.Simulation == null) { Application.Quit(1); yield break; }
            session.Simulation.SetPaused(true);
            var defs = session.Simulation.Towers.Select(t => t.Definition).Where(t => !t.IsWall).ToArray();
            var laser = defs.Single(t => t.Kind == TowerKind.Laser);
            Check(laser.Area == AttackArea.PositiveX && laser.Range > 10, "level 6 laser faces road and reaches road end");
            var originals = session.Towers.Where(t => !t.IsWall).SelectMany(t => t.GetComponentsInChildren<MeshRenderer>()).ToArray();
            Check(originals.Any(r => r.sharedMaterial.name.Contains("Gunmetal")) && originals.Any(r => r.sharedMaterial.name.Contains("Stone")), "authored stone and metal materials retained");
            Check(defs.Single(t => t.Kind == TowerKind.Frost).Damage == defs.First(t => t.Kind == TowerKind.Standard && t.Area == AttackArea.Square).Damage, "frost has normal sentry damage");
            Check(defs.Where(t => t.Kind == TowerKind.Blast).All(t => t.Damage == defs.First(x => x.Kind == TowerKind.Standard && x.Area == AttackArea.Square).Damage), "blast has normal sentry damage");
            // Use real player-side rendering callbacks with a deterministic durable squad.
            var unit = new UnitDefinition(100, .1f, UnitRole.Tank, healPerSecond: 0);
            var sim = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0,.25f,0), new Vector3(100,.25f,0) },
                new[] { new TowerDefinition(1, new Vector3(-2,.25f,0), 90,.6f,3,TargetRule.Nearest,
                    area:AttackArea.PositiveX,attackWidth:.9f,kind:TowerKind.Laser),
                    new TowerDefinition(2,new Vector3(0,.25f,-2),5,3,1,TargetRule.Nearest,kind:TowerKind.Blast),
                    new TowerDefinition(3,new Vector3(0,.25f,2),5,3,1,TargetRule.Nearest,kind:TowerKind.Frost) },
                new[] { new MemberSpawnDefinition(1,3,unit), new MemberSpawnDefinition(2,4,unit),
                    new MemberSpawnDefinition(3,5,unit),new MemberSpawnDefinition(4,0,unit) }));
            typeof(BattleSession).GetProperty("Simulation").SetValue(session, sim);
            session.enabled = false;
            var explosions = 0; sim.BlastImpact += _ => explosions++;
            var health = 0f;
            for (var step = 0; step < 220; step++)
            {
                sim.Step(.02f);
                // A real rendered frame for every tick tests the effect creation code in the packaged player.
                yield return null;
                if (step == 45)
                    Check(session.GetComponent<TowerSpecialEffects>().GetComponentsInChildren<LineRenderer>(true)
                        .Any(l => l.name == "White beam core" && l.enabled), "visible full-row laser core rendered");
            }
            foreach (var u in sim.Units) health += u.Hp;
            Check(explosions > 0 && health < 390, "blast effects and actual HP damage occur without exceptions");
            Check(sim.Units.Any(u => u.SlowRemaining > 0), "frost slow is applied");
            Directory.CreateDirectory("/tmp/team5-v2-smoke");
            File.WriteAllText("/tmp/team5-v2-smoke/result.txt", errors ? "FAIL" : "PASS");
            Debug.Log(errors ? "TOWER PLAYER SMOKE FAILED" : "TOWER PLAYER SMOKE PASSED");
            Application.logMessageReceived -= OnLog;
            Application.Quit(errors ? 1 : 0);
        }
    }
}
