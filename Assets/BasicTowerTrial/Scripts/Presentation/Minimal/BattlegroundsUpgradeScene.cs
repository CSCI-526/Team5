using System;
using System.Linq;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class BattlegroundsUpgradeScene : MonoBehaviour
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/BattlegroundsUpgrade.unity";
        public static TavernRecruitment State { get; private set; }
        private RectTransform root;
        private TMP_Text timer;
        private float seconds = 75;
        private int selected;
        private UnitRole? inspectedOffer;
        private bool leaving;
        private static readonly Color Gold = new Color(1,.78f,.32f), Ink = new Color(.14f,.09f,.08f), Wood = new Color(.24f,.15f,.12f), Paper = new Color(.91f,.83f,.65f);
        public Canvas Canvas { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() => State = null;
        public static void NewGame(int seed = -1) => State = new TavernRecruitment(seed);
        private void Awake()
        {
            if (State == null) NewGame();
            if (EventSystem.current == null)
            {
                var input = new GameObject("Tavern Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Draw();
        }
        private void Update()
        {
            if (leaving || State.Finished) return;
            if (State.Discover.Count == 0) seconds = Mathf.Max(0, seconds - Time.unscaledDeltaTime);
            if (timer != null) timer.text = $"{Mathf.CeilToInt(seconds)}s";
            if (seconds <= 0 && State.Ready) BeginAssault();
        }
        private static readonly Vector2[] Bevel={new Vector2(-.87f,-1),new Vector2(.87f,-1),new Vector2(1,-.75f),new Vector2(1,.75f),new Vector2(.87f,1),new Vector2(-.87f,1),new Vector2(-1,.75f),new Vector2(-1,-.75f)};
        private static readonly Vector2[] Shield={new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-.55f),new Vector2(0,-1),new Vector2(-1,-.55f)};
        private static readonly Vector2[] Arch={new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,.25f),new Vector2(.65f,.8f),new Vector2(0,1),new Vector2(-.65f,.8f),new Vector2(-1,.25f)};
        private static readonly Vector2[] Board={new Vector2(-1,-1),new Vector2(-1,1),new Vector2(-.6f,1),new Vector2(-.2f,.88f),new Vector2(0,.86f),new Vector2(.2f,.88f),new Vector2(.6f,1),new Vector2(1,1),new Vector2(1,-1),new Vector2(.43f,-1),new Vector2(.16f,-.9f),new Vector2(0,-.68f),new Vector2(-.16f,-.9f),new Vector2(-.43f,-1)};
        private RectTransform Shape(Transform parent,string name,float x,float y,float w,float h,Color color,Vector2[] points)
        {
            var rect=LegacyUiKit.CreateRect(parent,name,new Vector2(x,y),new Vector2(w,h));
            var graphic=rect.gameObject.AddComponent<TavernPolygonGraphic>();graphic.color=color;graphic.raycastTarget=false;graphic.SetShape(points);return rect;
        }
        private RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,Color color,float radius=12)
            => Shape(parent,name,x,y,w,h,color,Bevel);
        private Button Clickable(Graphic graphic)
        {
            graphic.raycastTarget=true;var button=graphic.gameObject.AddComponent<Button>();button.targetGraphic=graphic;
            button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
        }
        private TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color,bool bold=false)
        {
            var label=LegacyUiKit.CreateText(parent,name,value,new Vector2(x,y),new Vector2(w,h),size,color,bold?FontStyles.Bold:FontStyles.Normal);
            label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        private Button Button(Transform parent,string name,string value,float x,float y,float w,float h,Action action,bool enabled=true)
        {
            var special=name=="Upgrade"||name=="Refresh"||name=="Freeze";
            var frame=Shape(parent,name,x,y,w,h,new Color(.27f,.19f,.12f),special?Shield:Bevel);
            Shape(frame,"Button Rim",0,0,w-4,h-4,special?new Color(.56f,.67f,.25f):new Color(.67f,.49f,.28f),special?Shield:Bevel);
            var face=Shape(frame,"Button Face",0,0,w-11,h-11,special?new Color(.34f,.16f,.38f):new Color(.27f,.23f,.28f),special?Shield:Bevel);
            var button=Clickable(frame.GetComponent<Graphic>());
            Text(frame,"Label",value,0,special?5:0,w-16,h-8,special?15:18,Gold,true);
            button.interactable=enabled;button.gameObject.AddComponent<CanvasGroup>().alpha=enabled?1f:.45f;button.onClick.AddListener(()=>action());return button;
        }
        private void Draw()
        {
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            var host=new GameObject("Battlegrounds Tavern UI",typeof(RectTransform));root=(RectTransform)host.transform;
            root.SetParent(transform,false);
            Canvas=LegacyUiKit.CreateCanvas(host,50,new Vector2(1600,900),.5f);
            Canvas.renderMode=RenderMode.ScreenSpaceCamera;Canvas.worldCamera=GetComponentInChildren<Camera>();Canvas.planeDistance=1;
            Panel(root,"Background",0,0,1600,900,new Color(.12f,.09f,.14f),0);
            Shape(root,"Board Shadow",0,-13,1508,862,new Color(.06f,.04f,.06f),Board);
            Shape(root,"Board Frame",0,0,1490,850,new Color(.59f,.47f,.34f),Board);
            Shape(root,"Frame Inset",0,0,1450,810,new Color(.30f,.22f,.17f),Board);
            Shape(root,"Table",0,2,1412,772,new Color(.65f,.45f,.28f),Board);
            for(var plank=0;plank<8;plank++)Panel(root,"Wood Line "+plank,0,285-plank*66,1360,2,new Color(.27f,.16f,.10f,.16f),0);
            for(var side=-1;side<=1;side+=2)
            {
                var wing=new[]{new Vector2(-1*side,1),new Vector2(.7f*side,.4f),new Vector2(1*side,-.3f),new Vector2(1*side,-1),new Vector2(-1*side,-1)};
                Shape(root,"Lower Wing "+side,side*447,-303,515,222,new Color(.20f,.24f,.34f),wing);
                var shoulder=new[]{new Vector2(-1*side,1),new Vector2(.9f*side,.8f),new Vector2(1*side,-.8f),new Vector2(-1*side,-1)};
                Shape(root,"Upper Shoulder "+side,side*550,310,250,160,new Color(.48f,.31f,.20f),shoulder);
                for(var line=0;line<3;line++)
                {
                    var stripe=Panel(root,"Wing Inlay "+side+line,side*(465+line*12),-300-line*28,380-line*45,3,new Color(.38f,.42f,.51f,.45f),0);
                    stripe.localRotation=Quaternion.Euler(0,0,side*12);
                    var carving=Panel(root,"Shoulder Inlay "+side+line,side*553,345-line*28,190-line*22,3,new Color(.70f,.49f,.28f),0);
                    carving.localRotation=Quaternion.Euler(0,0,side*-14);
                }
            }
            Text(root,"Title","TAVERN",-565,365,220,50,30,Paper,true);
            Button(root,"Upgrade",State.Tier==6?"TIER 6 / MAX":$"TIER {State.Tier}\nUP {State.UpgradeCost}G",-155,365,125,108,()=>{State.Upgrade();Draw();},State.Tier<6&&State.Gold>=State.UpgradeCost);
            var shopSeal=Shape(root,"Shop Seal",0,365,152,146,new Color(.22f,.16f,.13f),Arch);
            Shape(shopSeal,"Seal Rim",0,0,140,134,new Color(.73f,.55f,.31f),Arch);
            Shape(shopSeal,"Seal Face",0,0,128,122,new Color(.24f,.20f,.18f),Arch);
            LegacyUiKit.CreateCircle(shopSeal,"Keeper Head",new Vector2(0,18),32,new Color(.72f,.47f,.31f));
            Shape(shopSeal,"Keeper Apron",0,-13,65,48,new Color(.82f,.80f,.67f),new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(.6f,1),new Vector2(-.6f,1)});
            Text(shopSeal,"Shop Label","SHOP",0,-44,110,25,17,Gold,true);
            Button(root,"Refresh","REFRESH\n1G",170,365,110,108,()=>{State.Roll();Draw();},State.Gold>=1);
            Button(root,"Freeze",State.Frozen?"UNFREEZE":"FREEZE",300,365,110,108,()=>{State.ToggleFreeze();Draw();});
            if(State.Finished)
            {
                Text(root,"Result",State.Lives>0?"EXPEDITION COMPLETE":"EXPEDITION ENDED",0,50,1100,70,38,Gold,true);
                Button(root,"New Game","NEW EXPEDITION",0,-70,400,65,()=>{NewGame();seconds=75;selected=0;Draw();});return;
            }
            var count=State.Offers.Count;var width=166f;
            for(var i=0;i<count;i++)
            {
                var index=i;var x=(i-(count-1)*.5f)*188;
                var card=Shape(root,"Offer "+i,x,220,width,184,new Color(.26f,.20f,.17f),Arch);
                Shape(card,"Card Rim",0,0,width-5,179,new Color(.76f,.64f,.39f),Arch);
                Shape(card,"Card Face",0,0,width-13,171,Paper,Arch);
                if(!State.Offers[i].HasValue){Text(card,"Sold","RECRUITED",0,0,width,55,15,Ink,true);continue;}
                var role=State.Offers[i].Value;var stats=SquadCombatRules.Create(role);
                var inspect=Clickable(card.GetComponent<Graphic>());
                inspect.onClick.AddListener(()=>{inspectedOffer=role;selected=0;Draw();});
                card.gameObject.AddComponent<TavernDragItem>().Bind(this,0,index);
                var pennant=Shape(card,"Tier Pennant",0,85,30,37,new Color(.37f,.17f,.40f),Shield);
                Text(pennant,"Tier",TavernRecruitment.RoleTier(role).ToString(),0,3,28,25,16,Gold,true);
                DrawPortrait(card,role,0,27,54);
                Text(card,"Name",LegacyUnitLooks.Name(role),0,-21,width-8,25,14,Ink,true);
                StatBadge(card,"Attack",stats.Damage,-55,-53,new Color(.82f,.62f,.20f));
                StatBadge(card,"Health",stats.MaxHp,55,-53,new Color(.65f,.20f,.19f));
                Button(card,"Buy","3G",0,-77,65,29,()=>BuyOffer(index),State.CanBuy(index));
            }
            Panel(root,"Shop Divider",0,122,1290,2,new Color(.22f,.13f,.08f,.45f),0);
            Text(root,"Formation Header",$"SQUAD  {State.DeployedCount} / 6",0,104,320,25,17,Paper,true);
            for(var slot=0;slot<9;slot++)
            {
                var target=slot;var x=(slot%3-1)*104;var y=50-(slot/3)*96;
                var cell=Panel(root,"Formation Slot "+slot,x,y,98,90,new Color(.27f,.23f,.20f,.72f));
                var follower=State.Followers.Find(f=>f.Slot==slot);
                if(follower==null)Button(cell,"Place","+",0,0,94,86,()=>PlaceSelected(target),selected!=0);
                else OwnedCard(cell,follower,0,0,94,86,()=>{if(selected!=0&&selected!=follower.Id)PlaceSelected(target);else{selected=follower.Id;inspectedOffer=null;Draw();}});
                cell.gameObject.AddComponent<TavernDropTarget>().Bind((id,offer)=>Drop(id,offer,target));
            }
            var picked=State.Followers.Find(f=>f.Id==selected);
            var inspection=picked!=null?State.Stats(picked):inspectedOffer.HasValue?SquadCombatRules.Create(inspectedOffer.Value):null;
            if(inspection!=null)
            {
                var details=Panel(root,"Unit Details",-445,-30,280,222,new Color(.23f,.15f,.12f,.86f));
                DrawPortrait(details,inspection.Role,0,63,52);
                Text(details,"Name",(picked!=null&&picked.Golden?"GOLDEN ":"")+LegacyUnitLooks.Name(inspection.Role),0,15,260,28,17,Gold,true);
                Text(details,"Effect",Description(inspection,false),0,-50,250,95,15,Paper);
            }
            else Text(root,"Hint","Drag units to your squad.\nThree copies become golden.",-445,-30,260,100,18,Paper);
            var clock=Shape(root,"Clock",580,55,118,78,new Color(.25f,.22f,.19f),Bevel);
            Shape(clock,"Clock Rim",0,0,108,68,new Color(.66f,.61f,.47f),Bevel);
            Shape(clock,"Clock Face",0,0,98,58,new Color(.20f,.23f,.24f),Bevel);
            timer=Text(clock,"Timer",$"{Mathf.CeilToInt(seconds)}s",0,0,95,55,32,Paper,true);
            Button(root,"Bench Selected","TO BENCH",475,-55,180,44,()=>{State.Bench(selected);selected=0;Draw();},picked!=null&&picked.Slot>=0);
            var sell=Button(root,"Sell","SELL  +1G",475,-111,180,44,()=>{State.Sell(selected);selected=0;Draw();},picked!=null&&picked.Role!=UnitRole.Captain);
            sell.gameObject.AddComponent<TavernDropTarget>().Bind((id,offer)=>{if(id!=0){State.Sell(id);selected=0;Draw();}});
            Text(root,"Notice",State.Message,-460,-160,360,48,13,Ink);
            var bench=Panel(root,"Bench",0,-263,975,118,new Color(.20f,.20f,.25f),20);
            bench.GetComponent<Graphic>().raycastTarget=true;
            bench.gameObject.AddComponent<TavernDropTarget>().Bind((id,offer)=>Drop(id,offer,-1));
            var reserves=State.Followers.Where(f=>f.Slot<0).ToArray();
            for(var i=0;i<7;i++)
            {
                var x=(i-3)*135;Panel(bench,"Bench Slot "+i,x,0,127,104,new Color(.27f,.27f,.32f));
                if(i<reserves.Length)OwnedCard(bench,reserves[i],x,0,123,100);
            }
            Text(root,"Gold",$"{State.Gold} / 10",565,-393,240,27,20,Gold,true);
            for(var i=0;i<10;i++)LegacyUiKit.CreateCircle(root,"Gold Coin "+i,new Vector2(442+i*27,-422),19,i<State.Gold?Gold:new Color(.25f,.18f,.12f));
            var hero=Shape(root,"Player Portrait",0,-357,164,176,new Color(.22f,.16f,.14f),Arch);
            Shape(hero,"Player Rim",0,0,150,162,new Color(.68f,.51f,.30f),Arch);
            Shape(hero,"Player Face",0,0,135,146,new Color(.30f,.24f,.35f),Arch);
            DrawPortrait(hero,UnitRole.Captain,0,0,79);
            var life=LegacyUiKit.CreateCircle(root,"Player Health",new Vector2(74,-401),42,new Color(.66f,.17f,.17f));
            Text(life.transform,"Lives",State.Lives.ToString(),0,0,40,35,24,Paper,true);
            Text(root,"Turn",$"TURN {Mathf.Min(State.Turn,8)} / 8",-510,-375,240,35,20,Paper,true);
            Text(root,"Bench Count",$"BENCH {State.BenchCount} / 7",0,-194,220,20,13,Ink);
            Button(root,"Begin Assault","READY",508,-350,112,46,BeginAssault,State.Ready);
            Button(root,"Formation Trial","FORMATION TRIAL",638,-350,138,46,BeginFormationTrial,State.Ready);
            if(State.Discover.Count>0)DrawDiscovery();
        }
        private void StatBadge(Transform parent,string name,float value,float x,float y,Color color)
        {
            var badge=Shape(parent,name,x,y,36,34,color,Bevel);
            Text(badge,"Value",value.ToString("0.#"),0,0,34,29,14,Color.white,true);
        }
        private static string Description(UnitDefinition stats,bool compact)
        {
            if(stats.Role==UnitRole.Captain)return compact?"Unique starting commander":"Unique commander. Arrange your 3x3 squad during preparation. The Captain cannot be sold.";
            if(stats.Role==UnitRole.Frostweaver)return compact?"Active: freeze nearest tower area":"Use Time Lock in combat to freeze the tower area nearest this unit. Cooldown: 30 seconds.";
            return compact?LegacyUnitIntroductionUI.ShortDescription(stats.Role):LegacyUnitIntroductionUI.LongDescription(stats);
        }
        private void DrawPortrait(Transform parent,UnitRole role,float x,float y,float size)
        {
            var ring=LegacyUiKit.CreateCircle(parent,"Portrait",new Vector2(x,y),size,LegacyUnitLooks.For(role).Body);
            LegacyUnitIconUI.Create(ring.transform,Vector2.zero,size*1.5f).SetType(role);
        }
        private void OwnedCard(Transform parent,TavernRecruitment.Follower unit,float x,float y,float w,float h,Action clicked=null)
        {
            var body=Shape(parent,"Unit "+unit.Id,x,y,w,h,selected==unit.Id?new Color(.65f,.48f,.18f):unit.Golden?new Color(.48f,.34f,.12f):new Color(.32f,.25f,.20f),Arch);
            var button=Clickable(body.GetComponent<Graphic>());
            DrawPortrait(button.transform,unit.Role,0,h*.20f,38);
            Text(button.transform,"Name",(unit.Golden?"GOLDEN ":"")+LegacyUnitLooks.Name(unit.Role),0,-h*.16f,w-6,24,10,Paper,true);
            var stats=State.Stats(unit);Text(button.transform,"Stats",$"{stats.Damage:0.#} ATK  {stats.MaxHp:0.#} HP",0,-h*.36f,w-6,18,11,Gold,true);
            button.onClick.AddListener(()=>{if(clicked!=null)clicked();else{selected=unit.Id;inspectedOffer=null;Draw();}});
            button.gameObject.AddComponent<TavernDragItem>().Bind(this,unit.Id,-1);
        }
        private void DrawDiscovery()
        {
            var shade=Panel(root,"Discover Overlay",0,0,1600,900,new Color(0,0,0,.9f));shade.GetComponent<Graphic>().raycastTarget=true;
            Text(shade,"Title","TRIPLE REWARD  /  DISCOVER A UNIT",0,210,1100,65,34,Gold,true);
            Text(shade,"Hint","Choose one free unit. Preparation timer is paused.",0,148,1000,35,20,Paper);
            for(var i=0;i<State.Discover.Count;i++)
            {
                var index=i;var role=State.Discover[i];var x=(i-(State.Discover.Count-1)*.5f)*290;
                var panel=Panel(shade,"Discover "+i,x,-15,265,240,Paper);
                DrawPortrait(panel,role,0,55,85);
                Text(panel,"Name",LegacyUnitLooks.Name(role),0,-15,250,40,22,Ink,true);
                Text(panel,"Tier","TIER "+TavernRecruitment.RoleTier(role),0,-52,250,30,18,Ink);
                Button(panel,"Choose","CHOOSE",0,-98,235,40,()=>{State.ChooseDiscovery(index);selected=State.LastAddedId;Draw();});
            }
        }
        public void BuyOffer(int index)
        {
            if(State.Buy(index))selected=State.LastAddedId;Draw();
        }
        private void PlaceSelected(int slot){if(selected!=0&&State.Place(selected,slot))selected=0;Draw();}
        private void Drop(int id,int offer,int slot)
        {
            if(offer>=0){if(!State.Buy(offer))return;id=State.LastAddedId;}
            if(id==0)return;
            if(slot<0)State.Bench(id);else State.Place(id,slot);
            selected=id;Draw();
        }
        private void BeginFormationTrial()
        {
            if (leaving || !State.Ready) return;
            leaving = true;
            FormationReserveBattle.EnterFromTavern(State);
        }

        public void BeginAssault()
        {
            if(leaving||!State.Ready)return;leaving=true;SiegeTavernMode.UseRecruitment(State);
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SiegeTavernMode.BattleScene,new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SiegeTavernMode.BattleScene);
#endif
        }
    }

    public sealed class TavernDragItem : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public int MemberId { get; private set; }
        public int OfferIndex { get; private set; }
        private BattlegroundsUpgradeScene owner;private CanvasGroup group;private RectTransform ghost;
        public void Bind(BattlegroundsUpgradeScene view,int id,int offer){owner=view;MemberId=id;OfferIndex=offer;}
        public void OnBeginDrag(PointerEventData data)
        {
            group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.alpha=.45f;
            ghost=LegacyUiKit.CreateRect(owner.Canvas.transform,"Dragged Unit",Vector2.zero,new Vector2(145,65));
            var text=LegacyUiKit.CreateText(ghost,"Label",OfferIndex>=0?"RECRUIT  3G":"MOVE UNIT",Vector2.zero,new Vector2(145,65),22,Color.yellow,FontStyles.Bold);
            text.raycastTarget=false;OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if(ghost!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)owner.Canvas.transform,data.position,owner.Canvas.worldCamera,out var point))ghost.anchoredPosition=point;
        }
        public void OnEndDrag(PointerEventData data){if(group!=null){group.blocksRaycasts=true;group.alpha=1;Destroy(group);}if(ghost!=null)Destroy(ghost.gameObject);}
    }
    public sealed class TavernDropTarget : MonoBehaviour,IDropHandler
    {
        private Action<int,int> action;
        public void Bind(Action<int,int> callback)=>action=callback;
        public void OnDrop(PointerEventData data){var card=data.pointerDrag!=null?data.pointerDrag.GetComponent<TavernDragItem>():null;if(card!=null)action?.Invoke(card.MemberId,card.OfferIndex);}
    }
}
