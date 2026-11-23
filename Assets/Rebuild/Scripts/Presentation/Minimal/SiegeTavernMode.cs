using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation
{
    [DefaultExecutionOrder(-200)]
    public sealed class SiegeTavernMode : MonoBehaviour
    {
        public const string ShopScene="Assets/Rebuild/Scenes/SiegeTavernUpgrade.unity",BattleScene="Assets/Rebuild/Scenes/SiegeTavernBattle.unity";
        public static SiegeTavernRun Run {get;private set;}
        private static TavernRecruitment recruitment;
        public static void UseRecruitment(TavernRecruitment state)
        {
            recruitment=state;
            Run=new SiegeTavernRun{Round=state.Turn,Lives=state.Lives};
        }
        [SerializeField] private bool shopping;
        [SerializeField] private BattleSession session;
        public SiegeEnemyBuilder EnemyBuilder { get; private set; }
        private Team5.Rebuild.World.BattleTower[] defenseTowers;
        private Renderer[][] defenseRenderers;
        private Vector3[] defenseScales;
        private double lastSimulationTime;
        private float remaining=60,combatTime;private int selected;private bool leaving;private GUIStyle title,body,button,small;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatics(){Run=null;recruitment=null;}
        public static void NewRun(){recruitment=null;Run=new SiegeTavernRun();}
        private void Awake(){if(Run==null)NewRun();}
        private void Start()
        {
            if(shopping)return;
            // Direct Play on the combat scene also has a valid starter team.
            var towers=session.Towers.Where(t=>!t.IsWall).ToArray();var count=Mathf.Min(towers.Length,1+Run.Round);
            for(var i=count;i<towers.Length;i++)towers[i].gameObject.SetActive(false);
            // BattleSession validates only active tower references, so unused building sites
            // retain their platform but are removed from this prototype session's list.
            var active=towers.Take(count).ToArray();ConfigureActiveTowers(active);
            session.SetPreparedMembers(recruitment!=null?recruitment.Deployment():Run.Deployment());session.StartBattle();
            if(session.Simulation==null)return;
            defenseTowers=active;
            defenseRenderers=active.Select(t=>t.GetComponentsInChildren<Renderer>(true)).ToArray();
            defenseScales=active.Select(t=>t.transform.localScale).ToArray();
            EnemyBuilder=new SiegeEnemyBuilder(session.Simulation,session.Simulation.Towers.Select(t=>t.Id).ToArray());
            RefreshDefenseVisuals();
            var inspector=session.GetComponentInChildren<BattleUnitInspection>();if(inspector!=null)inspector.IsInputSuppressed=true;
        }
        private void ConfigureActiveTowers(Team5.Rebuild.World.BattleTower[] active)
        {
            // Runtime configuration is exposed through a small prototype-only adapter on Session.
            session.SetPrototypeTowerRoster(active);
        }
        private void Update()
        {
            if(leaving||Run.Finished)return;
            if(shopping){remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);if(remaining<=0)BeginBattle();}
            else if(session.Simulation!=null)
            {
                var elapsed=session.Simulation.ElapsedTime;
                EnemyBuilder?.Tick((float)(elapsed-lastSimulationTime));lastSimulationTime=elapsed;
                combatTime=(float)elapsed;
                RefreshDefenseVisuals();
                var outcome=session.Simulation.Outcome;
                if(outcome!=BattleOutcome.Running||combatTime>=90){FinishAssault(outcome==BattleOutcome.Victory);}
            }
        }
        private void FinishAssault(bool victory)
        {
            if(leaving)return;leaving=true;
            if(recruitment!=null){recruitment.Resolve(victory);Load(BattlegroundsUpgradeScene.ScenePath);}
            else{Run.Resolve(victory);Load(ShopScene);}
        }
        private void RefreshDefenseVisuals()
        {
            if(defenseTowers==null)return;
            for(var i=0;i<defenseTowers.Length;i++)
            {
                var state=defenseTowers[i].RuntimeState;
                foreach(var renderer in defenseRenderers[i])if(renderer!=null)renderer.enabled=state!=null&&state.IsAlive;
                var growth=1f+.12f*Mathf.Max(0,EnemyBuilder.Levels[i]-1);
                defenseTowers[i].transform.localScale=Vector3.Scale(defenseScales[i],new Vector3(1,growth,1));
            }
        }
        public void BeginBattle(){if(leaving||Run.Finished||Run.Deployment().Count==0)return;leaving=true;Load(BattleScene);}
        private static void Load(string path)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
        }
        private void Styles()
        {
            if(title!=null)return;title=new GUIStyle(GUI.skin.label){fontSize=30,fontStyle=FontStyle.Bold,normal={textColor=new Color(1,.83f,.42f)}};
            body=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};small=new GUIStyle(body){fontSize=14};
            button=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true};
        }
        private bool Button(Rect r,string text,bool enabled=true){var old=GUI.enabled;GUI.enabled=enabled;var hit=GUI.Button(r,text,button);GUI.enabled=old;return hit;}
        private void Label(Rect r,string text,bool mini=false)=>GUI.Label(r,text,mini?small:body);
        private void OnGUI()
        {
            if(Run==null)return;Styles();var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            try
            {
                if(!shopping)
                {
                    GUI.Box(new Rect(0,0,1280,75),"");GUI.Label(new Rect(25,12,650,45),"SIEGE EXPEDITION  /  ASSAULT "+Run.Round,title);
                    Label(new Rect(690,20,310,40),$"Lives {Run.Lives}   Time {Mathf.CeilToInt(90-combatTime)}s");
                    if(Button(new Rect(1020,15,110,42),session.Simulation?.IsPaused==true?"Resume":"Pause"))session.Simulation?.SetPaused(!session.Simulation.IsPaused);
                    if(Button(new Rect(1140,15,115,42),"Retreat",!leaving)){FinishAssault(false);}
                    GUI.Box(new Rect(20,85,820,62),"");
                    Label(new Rect(32,91,800,25),$"ENEMY BUILDERS | Gold {EnemyBuilder?.Gold:0} | {EnemyBuilder?.LastAction}",true);
                    Label(new Rect(32,116,800,25),"Empty sites -> build (8 gold) -> upgrade to level 3. Enemy earns 2 gold / second.",true);
                    if(defenseTowers!=null&&Camera.main!=null)for(var i=0;i<defenseTowers.Length;i++)
                    {
                        var state=defenseTowers[i].RuntimeState;
                        if(state==null||state.Hp<=0)continue;
                        var point=Camera.main.WorldToScreenPoint(defenseTowers[i].transform.position+Vector3.up*1.8f);
                        if(point.z<=0)continue;
                        var x=point.x/Screen.width*1280;var y=(1-point.y/Screen.height)*720;
                        GUI.Box(new Rect(x-55,y-20,110,40),state.UnderConstruction?"BUILD SITE":$"Tower Lv.{EnemyBuilder.Levels[i]}\nHP {state.Hp:0}/{state.Definition.MaxHp:0}");
                    }
                    if(Camera.main!=null&&session.Simulation!=null)foreach(var unit in session.Simulation.Units)
                    {
                        if(!unit.IsAlive)continue;
                        var point=Camera.main.WorldToScreenPoint(unit.Position+Vector3.up*1.3f);
                        if(point.z<=0)continue;
                        var x=point.x/Screen.width*1280;var y=(1-point.y/Screen.height)*720;
                        var color=GUI.color;GUI.color=Color.black;GUI.DrawTexture(new Rect(x-18,y,36,5),Texture2D.whiteTexture);
                        GUI.color=Color.green;GUI.DrawTexture(new Rect(x-17,y+1,34*unit.Hp/unit.MaxHp,3),Texture2D.whiteTexture);GUI.color=color;
                    }
                    if(recruitment!=null&&session.Simulation!=null)
                    {
                        var abilityIndex=0;
                        foreach(var caster in session.Simulation.Units.Where(u=>u.IsAlive&&LegendarySkillRules.HasActiveSkill(u.Role)))
                        {
                            if(Button(new Rect(20+abilityIndex++*225,650,215,45),LegendarySkillRules.ShortName(caster.Role),session.Simulation.CanUseLegendarySkill(caster.Id)))
                            {
                                var target=session.Simulation.Towers.Where(t=>t.IsAlive).OrderBy(t=>(t.Position-caster.Position).sqrMagnitude).FirstOrDefault();
                                session.Simulation.TryUseLegendarySkill(caster.Id,target!=null?target.Position:caster.Position);
                            }
                        }
                    }
                    return;
                }
                GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.055f,.075f,.11f),0,0);
                GUI.Label(new Rect(30,20,800,45),"THE SIEGE TAVERN",title);
                Label(new Rect(30,75,1190,35),$"Round {Mathf.Min(Run.Round,5)}/5    Lives {Run.Lives}    Gold {Run.Gold}/15    Tavern {Run.Tier}/3    Preparation {Mathf.CeilToInt(remaining)}s");
                Label(new Rect(30,110,1200,40),Run.Result,true);
                if(Run.Finished){if(Button(new Rect(440,310,400,70),"Start a new expedition")){NewRun();remaining=60;selected=0;}return;}
                Label(new Rect(30,155,790,30),$"NEXT DEFENSE: {Mathf.Min(4,1+Run.Round)} empty sites. Enemy builds towers and upgrades them during combat.",true);
                for(var i=0;i<Run.Offers.Length;i++)
                {
                    var r=new Rect(30+i*195,195,180,225);GUI.Box(r,"");var kind=Run.Offers[i];
                    if(kind<0){Label(new Rect(r.x+12,r.y+70,160,40),"RECRUITED");continue;}
                    var c=SiegeTavernRun.Catalog[kind];
                    var tint=GUI.color;GUI.color=LegacyUI.LegacyUnitLooks.For(c.Role).Body;
                    GUI.DrawTexture(new Rect(r.x+2,r.y+2,r.width-4,5),Texture2D.whiteTexture);GUI.color=tint;
                    Label(new Rect(r.x+12,r.y+12,160,50),c.Name);Label(new Rect(r.x+12,r.y+65,160,30),$"ATK {c.Attack} / HP {c.Hp}");
                    Label(new Rect(r.x+12,r.y+100,156,78),c.Effect,true);
                    if(Button(new Rect(r.x+10,r.y+180,160,34),"Recruit — 3 gold",Run.Gold>=3&&Run.Followers.Count<12))Run.Buy(i);
                }
                if(Button(new Rect(30,435,180,40),"Refresh — 1 gold",Run.Gold>0))Run.Roll();
                if(Button(new Rect(225,435,180,40),Run.Frozen?"Unfreeze":"Freeze offers"))Run.Frozen=!Run.Frozen;
                if(Button(new Rect(420,435,180,40),"Tavern up — 5",Run.Tier<3&&Run.Gold>=5))Run.Upgrade();
                Label(new Rect(850,155,390,35),"3 x 3 FORMATION  /  max 6 deployed");
                for(var slot=0;slot<9;slot++)
                {
                    var f=Run.Followers.Find(x=>x.Slot==slot);var r=new Rect(850+(slot%3)*125,200+(slot/3)*78,115,68);
                    if(Button(r,f==null?"Slot "+(slot+1):SiegeTavernRun.Catalog[f.Kind].Name+"\nLv."+f.Level+(selected==f.Id?" *":"")))
                    {if(selected!=0){Run.Place(selected,slot);selected=0;}else if(f!=null)selected=f.Id;}
                }
                Label(new Rect(30,495,1200,30),"ROSTER — select a follower, then a formation slot to deploy or swap.  * = selected",true);
                for(var i=0;i<Run.Followers.Count;i++)
                {
                    var f=Run.Followers[i];if(Button(new Rect(30+i%6*132,535+i/6*55,124,48),SiegeTavernRun.Catalog[f.Kind].Name+(f.Slot<0?" [bench]":" [field]")+(selected==f.Id?" *":"")))selected=f.Id;
                }
                if(Button(new Rect(850,455,115,40),"Bench",selected!=0)){Run.Bench(selected);selected=0;}
                if(Button(new Rect(975,455,115,40),"Sell +1",selected!=0)){Run.Sell(selected);selected=0;}
                if(Button(new Rect(1100,455,115,40),"Train — 3",selected!=0&&Run.Gold>=3))Run.Train(selected);
                var picked=Run.Followers.Find(f=>f.Id==selected);
                if(picked!=null)
                {
                    var definition=Run.Deployment().Find(m=>m.MemberId==picked.Id)?.Unit;
                    var recruit=SiegeTavernRun.Catalog[picked.Kind];var growth=1+.3f*(picked.Level-1);
                    Label(new Rect(850,505,365,65),$"{recruit.Name} Lv.{picked.Level}  ATK {definition?.Damage??recruit.Attack*growth:0.0} / HP {definition?.MaxHp??recruit.Hp*growth:0.0}\n{recruit.Effect}",true);
                }
                if(Button(new Rect(850,575,365,65),"BEGIN ASSAULT",Run.Deployment().Count>0))BeginBattle();
                Label(new Rect(850,650,370,50),"Followers recover after battle. Formation bonuses are fixed at deployment.",true);
            }
            finally{GUI.matrix=old;}
        }
    }
}
