using System;
using System.Linq;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed partial class BattlegroundsUpgradeScene : MonoBehaviour
    {
        public const string ScenePath = "Assets/RandomMuster/Scenes/BattlegroundsUpgrade.unity";
        [SerializeField] private bool standalone;
        public static TavernRecruitment State { get; private set; }
        private RectTransform root;
        private TMP_Text timer;
        private const float PreparationSeconds = 20;
        private float seconds = PreparationSeconds;
        private int selected;
        private bool leaving;
        private TavernUnitHoverCard offerHoverCard;
        private static readonly Color Gold = new Color(1,.78f,.32f), Ink = new Color(.14f,.09f,.08f), Wood = new Color(.24f,.15f,.12f), Paper = new Color(.91f,.83f,.65f);
        public Canvas Canvas { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() => State = null;
        public static void NewGame(int seed = -1) => State = new TavernRecruitment(seed);
        private void Awake()
        {
            if (State == null) NewGame();
#if UNITY_EDITOR
            if (Application.isPlaying && applyTestGoldOnPlay) State.SetGoldForTesting(testGold);
#endif
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
            seconds = Mathf.Max(0, seconds - Time.unscaledDeltaTime);
            if (timer != null) timer.text = $"{Mathf.CeilToInt(seconds)}s";
            if (!standalone && seconds <= 0 && State.Ready) BeginAssault();
        }
        private static readonly Vector2[] Bevel={new Vector2(-.87f,-1),new Vector2(.87f,-1),new Vector2(1,-.75f),new Vector2(1,.75f),new Vector2(.87f,1),new Vector2(-.87f,1),new Vector2(-1,.75f),new Vector2(-1,-.75f)};
        private static readonly Vector2[] Shield={new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-.55f),new Vector2(0,-1),new Vector2(-1,-.55f)};
        private static readonly Vector2[] Arch={new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,.25f),new Vector2(.65f,.8f),new Vector2(0,1),new Vector2(-.65f,.8f),new Vector2(-1,.25f)};
        private static readonly Vector2[] RecruitFrame =
        {
            new Vector2(-.55f,-1), new Vector2(.55f,-1), new Vector2(.89f,-.65f),
            new Vector2(1,0), new Vector2(.83f,.68f), new Vector2(.48f,1),
            new Vector2(-.48f,1), new Vector2(-.83f,.68f), new Vector2(-1,0),
            new Vector2(-.89f,-.65f)
        };
        private static readonly Vector2[] TableOutline =
        {
            new Vector2(-.91f,-1), new Vector2(.91f,-1), new Vector2(1,-.86f),
            new Vector2(1,.87f), new Vector2(.91f,1), new Vector2(.47f,1),
            new Vector2(.26f,.96f), new Vector2(-.26f,.96f), new Vector2(-.47f,1),
            new Vector2(-.91f,1), new Vector2(-1,.87f), new Vector2(-1,-.86f)
        };
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
        private Button Button(Transform parent,string name,string value,float x,float y,float w,float h,Action action,bool enabled=true,Color? faceColor=null)
        {
            var frame=Shape(parent,name,x,y,w,h,new Color(.27f,.19f,.12f),Bevel);
            Shape(frame,"Button Rim",0,0,w-4,h-4,new Color(.67f,.49f,.28f),Bevel);
            Shape(frame,"Button Face",0,0,w-11,h-11,faceColor ?? new Color(.27f,.23f,.28f),Bevel);
            var button=Clickable(frame.GetComponent<Graphic>());
            Text(frame,"Label",value,0,0,w-16,h-8,18,Gold,true);
            button.interactable=enabled;button.gameObject.AddComponent<CanvasGroup>().alpha=enabled?1f:.45f;button.onClick.AddListener(()=>action());return button;
        }

        private Button AddSkipButton(Transform parent, float y, float width, float height)
            => Button(parent, "Skip countdown", "SKIP", 0, y, width, height, BeginAssault, State.Ready,
                faceColor: new Color(.68f, .13f, .16f));
        private void ShopBadge(string action, string label, string cost, float x, Action clicked, bool enabled)
        {
            var badgeOutline = new[]
            {
                new Vector2(-.33f,-1), new Vector2(.33f,-1), new Vector2(.76f,-.79f),
                new Vector2(.95f,-.49f), new Vector2(.95f,.58f), new Vector2(.76f,.83f),
                new Vector2(.38f,1), new Vector2(-.38f,1), new Vector2(-.76f,.83f),
                new Vector2(-.95f,.58f), new Vector2(-.95f,-.49f), new Vector2(-.76f,-.79f)
            };
            var icon = Shape(root, action, x, 335, 70, 79, Ink, badgeOutline);
            Shape(icon, "Outer rim", 0, 0, 66, 75, enabled ? new Color(.49f,.81f,.34f) : new Color(.56f,.48f,.41f), badgeOutline);
            Shape(icon, "Inner rim", 0, 0, 60, 69, new Color(.22f,.13f,.27f), badgeOutline);
            Shape(icon, "Purple face", 0, 0, 50, 59, new Color(.56f,.23f,.61f), badgeOutline);
            if (action == "Freeze")
            {
                for (var i = 0; i < 3; i++)
                    Panel(icon, "Frost spoke", 0, 3, 4, 29, Gold).localRotation = Quaternion.Euler(0, 0, i * 60);
            }
            else if (action == "Refresh")
            {
                const int segments = 24;
                var curve = new Vector2[(segments + 1) * 2];
                for (var i = 0; i <= segments; i++)
                {
                    var angle = (-143 + 240f * i / segments) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    curve[i] = direction;
                    curve[curve.Length - 1 - i] = direction * .54f;
                }
                var arrowhead = new[] { new Vector2(-1,0), new Vector2(.55f,-1), new Vector2(.73f,-.4f),
                    new Vector2(1,-.4f), new Vector2(1,.4f), new Vector2(.73f,.4f), new Vector2(.55f,1) };
                var shadow = new Color(.32f,.16f,.13f);
                Shape(icon, "Arrow shadow", 1, -2, 31, 31, shadow, curve);
                Shape(icon, "Arrowhead shadow", -4, 9, 22, 20, shadow, arrowhead);
                Shape(icon, "Gold arrow curve", 0, 0, 30, 30, Gold, curve);
                Shape(icon, "Gold arrowhead", -5, 11, 21, 19, Gold, arrowhead);
            }
            else
            {
                for (var i = 0; i < 2; i++)
                    Shape(icon, "Rank chevron", 0, -2 + 12 * i, 28, 11, Gold,
                        new[] { new Vector2(-1,-1), new Vector2(0,1), new Vector2(1,-1) });
            }
            var coin = LegacyUiKit.CreateCircle(icon, "Cost coin", new Vector2(0, 36), 29, new Color(.67f,.42f,.20f));
            LegacyUiKit.CreateCircle(coin.transform, "Coin face", Vector2.zero, 23, Gold);
            Text(coin.transform, "Cost", cost, 0, 0, 28, 26, 13, Ink, true);
            Text(icon, "Action label", label, 0, -47, 98, 19, 11, Paper, true);
            var button = Clickable(icon.GetComponent<Graphic>());
            button.interactable = enabled;
            button.onClick.AddListener(() => clicked());
        }
        private void Draw()
        {
            if (courierCanvas != null)
            {
                DrawCourierTavern();
                return;
            }
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            var host=new GameObject("Battlegrounds Tavern UI",typeof(RectTransform));root=(RectTransform)host.transform;
            root.SetParent(transform,false);
            Canvas=LegacyUiKit.CreateCanvas(host,50,new Vector2(1600,900),.5f);
            var canvasBackground = new Color(.11f,.08f,.13f);
            var slate = new Color(.15f,.22f,.29f);
            var panelBorder = new Color(.54f,.36f,.21f);
            Panel(root, "Background", 0, 0, 2200, 1300, canvasBackground);
            Shape(root, "Table shadow", 0, -11, 1250, 827, new Color(.05f,.04f,.06f), TableOutline);
            Shape(root, "Table frame", 0, 0, 1250, 827, panelBorder, TableOutline);
            Shape(root, "Frame bevel", 0, 0, 1230, 807, new Color(.70f,.53f,.37f), TableOutline);
            Shape(root, "Table recess", 0, 0, 1197, 774, new Color(.37f,.23f,.16f), TableOutline);
            Shape(root, "Table surface", 0, 0, 1175, 748, new Color(.73f,.53f,.36f), TableOutline);
            var leftCap = new[] { new Vector2(-1,1), new Vector2(1,1), new Vector2(.78f,.25f),
                new Vector2(-.72f,-1), new Vector2(-1,-.68f) };
            var rightCap = leftCap.Select(point => new Vector2(-point.x, point.y)).Reverse().ToArray();
            Shape(root, "Left timber corner", -351, 277, 472, 177, new Color(.39f,.25f,.17f), leftCap);
            Shape(root, "Right timber corner", 351, 277, 472, 177, new Color(.39f,.25f,.17f), rightCap);
            Shape(root, "Left upper inset", -351, 279, 440, 150, new Color(.52f,.33f,.21f), leftCap);
            Shape(root, "Right upper inset", 351, 279, 440, 150, new Color(.52f,.33f,.21f), rightCap);
            Text(root, "Title", "TAVERN", -446, 331, 250, 38, 24, Paper, true);
            var emblem = Shape(root, "Tavern emblem", 0, 321, 115, 111, panelBorder, Arch);
            Shape(emblem, "Emblem inset", 0, -2, 98, 94, canvasBackground, Arch);
            DrawPortrait(emblem, UnitRole.Captain, 0, -3, 64);
            ShopBadge("Upgrade", $"TIER {State.Tier}", State.Tier >= TavernRecruitment.MaxTier ? "MAX" : State.UpgradeCost.ToString(),
                -102, () => { State.Upgrade(); Draw(); }, State.CanUpgrade);
            ShopBadge("Refresh", "REFRESH", "1", 102, () => { State.Roll(); Draw(); }, State.Gold >= 1);
            ShopBadge("Freeze", State.Frozen ? "UNFREEZE" : "FREEZE", "0", 185,
                () => { State.ToggleFreeze(); Draw(); }, true);
            var leftWing = new[] { new Vector2(-1,-1), new Vector2(.82f,-1),
                new Vector2(1,-.48f), new Vector2(-.84f,1), new Vector2(-1,.77f) };
            var rightWing = leftWing.Select(point => new Vector2(-point.x, point.y)).Reverse().ToArray();
            Shape(root, "Left lower frame", -389, -269, 394, 199, slate, leftWing);
            Shape(root, "Right lower frame", 389, -269, 394, 199, slate, rightWing);
            Shape(root, "Left lower inset", -389, -269, 369, 175, new Color(.22f,.29f,.39f), leftWing);
            Shape(root, "Right lower inset", 389, -269, 369, 175, new Color(.22f,.29f,.39f), rightWing);
            foreach (var side in new[] { -1, 1 })
            {
                var plate = Panel(root, "Corner plate", side * 525, -324, 90, 43, new Color(.16f,.22f,.32f));
                plate.localRotation = Quaternion.Euler(0, 0, side * 11);
                Panel(plate, "Plate inset", 0, 0, 74, 28, new Color(.29f,.36f,.46f));
            }
            if(State.Finished)
            {
                Text(root,"Result",State.CaptainHealth>0?"EXPEDITION COMPLETE":"EXPEDITION ENDED",0,50,1100,70,38,Gold,true);
                Button(root,"New Game","NEW EXPEDITION",0,-70,400,65,()=>{NewGame();seconds=PreparationSeconds;selected=0;Draw();});return;
            }
            var count=State.Offers.Count;var width=124f;
            for(var i=0;i<count;i++)
            {
                var index=i;var x=(i-(count-1)*.5f)*132;
                Shape(root,"Offer shadow",x,180,width+7,166,new Color(.28f,.19f,.14f),RecruitFrame);
                var card=Shape(root,"Offer "+i,x,187,width,164,State.CanBuy(i) ? new Color(.24f,.63f,.66f) : new Color(.13f,.25f,.27f),RecruitFrame);
                Shape(card,"Card Rim",0,0,width-6,158,new Color(.75f,.63f,.35f),RecruitFrame);
                Shape(card,"Card Face",0,0,width-15,149,Paper,RecruitFrame);
                if(!State.Offers[i].HasValue){Text(card,"Sold","RECRUITED",0,0,width,55,15,Ink,true);continue;}
                var role=State.Offers[i].Value;
                var buy = Clickable(card.GetComponent<Graphic>());
                buy.interactable = State.CanBuy(index);
                buy.onClick.AddListener(() => BuyOffer(index));
                AttachOfferHover(card, index);
                var pennant=Shape(card,"Tier Pennant",0,76,32,33,new Color(.39f,.21f,.42f),Shield);
                Text(pennant,"Tier",TavernRecruitment.RoleTier(role).ToString(),0,3,28,25,16,Gold,true);
                DrawPortrait(card,role,0,27,54);
                AddOfferPrice(card, new Vector2(20, 47), Gold, TavernRecruitment.RecruitCost(role).ToString());
            }
            var clockFrame = Panel(root, "Clock frame", 566, 35, 112, 65, panelBorder);
            var clock = Panel(clockFrame, "Clock", 0, 0, 100, 53, slate);
            timer = Text(clock, "Timer", $"{Mathf.CeilToInt(seconds)}s", 0, 0, 95, 50, 26, Paper, true);
            if (!standalone) AddSkipButton(clockFrame, -58, 103, 42);

            var playerCrest = Shape(root, "Player crest", 0, -216, 101, 96, panelBorder, Arch);
            Shape(playerCrest, "Crest inset", 0, 0, 86, 81, slate, Arch);
            DrawPortrait(playerCrest, UnitRole.Captain, 0, 2, 49);
            var lives = LegacyUiKit.CreateCircle(playerCrest, "Lives badge", new Vector2(39, -29), 33, new Color(.58f,.20f,.22f));
            Text(lives.transform, "Health", State.CaptainHealth.ToString(), 0, 0, 31, 29, 17, Paper, true);
            var handFrame = Shape(root, "Hand frame", 0, -332, 762, 166, panelBorder, Arch);
            var handDock = Shape(handFrame, "Hand dock", 0, -3, 738, 147, slate, Arch);
            var reserves = State.Followers.Where(follower => follower.Slot < 0).ToArray();
            if (reserves.Length == 0)
                Text(handDock, "Empty hand", "Buy a unit to add it to your hand", 0, 23, 520, 32, 18, Paper);
            for (var i = 0; i < reserves.Length; i++)
            {
                var offset = i - (reserves.Length - 1) * .5f;
                var handCard = OwnedCard(handDock, reserves[i], offset * 83,
                    35 - offset * offset * 3 + (selected == reserves[i].Id ? 20 : 0), 126, 125);
                handCard.localRotation = Quaternion.Euler(0, 0, -offset * 6);
            }
            Text(root, "Hand count", $"HAND {reserves.Length} / 7", 0, -407, 220, 22, 14, Paper, true);
            var purse = Panel(root, "Gold purse", 455, -349, 198, 50, canvasBackground);
            LegacyUiKit.CreateCircle(purse, "Coin", new Vector2(-70, 0), 23, Gold);
            Text(purse, "Gold", $"{State.Gold} GOLD", 13, 0, 138, 34, 18, Gold, true);
            Text(root, "Notice", State.Message, 0, -122, 700, 28, 15, Ink);
        }
        private static string Description(UnitDefinition stats,bool compact)
        {
            if(stats.Role==UnitRole.Captain)return compact?"Unique starting commander":"Unique commander. Arrange your 3x3 squad during preparation. The Captain cannot be sold.";
            if(stats.Role==UnitRole.Frostweaver)return compact?"Active: freeze nearest tower area":"Use Time Lock in combat to freeze the tower area nearest this unit. Cooldown: 30 seconds.";
            return compact?LegacyUnitIntroductionUI.ShortDescription(stats.Role):LegacyUnitIntroductionUI.LongDescription(stats);
        }
        private void DrawPortrait(Transform parent,UnitRole role,float x,float y,float size,bool upgraded = false)
        {
            var ring=LegacyUiKit.CreateCircle(parent,"Portrait",new Vector2(x,y),size,LegacyUnitLooks.For(role).Body);
            LegacyUnitIconUI.Create(ring.transform,Vector2.zero,size*1.5f).SetType(role, upgraded);
        }
        private RectTransform OwnedCard(Transform parent,TavernRecruitment.Follower unit,float x,float y,float w,float h)
        {
            var body=Shape(parent,"Unit "+unit.Id,x,y,w,h,selected==unit.Id?new Color(.65f,.48f,.18f):new Color(.32f,.25f,.20f),Arch);
            var button=Clickable(body.GetComponent<Graphic>());
            DrawPortrait(button.transform,unit.Role,0,h*.20f,38,unit.Golden);
            if (unit.Golden)
                AttachUnitHover(body, () => (unit.Role, true));
            Text(button.transform,"Name",LegacyUnitLooks.Name(unit.Role),0,-h*.16f,w-6,24,10,Paper,true);
            button.onClick.AddListener(() => { selected = unit.Id; Draw(); });
            return body;
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
        private static void AddOfferPrice(Transform parent, Vector2 position, Color gold, string cost = "")
        {
            var rim = LegacyUiKit.CreateCircle(parent, "Cost badge", position, 23, gold);
            LegacyUiKit.CreateCircle(rim.transform, "White center", Vector2.zero, 19, Color.white);
            var number = LegacyUiKit.CreateText(rim.transform, "Cost", cost,
                Vector2.zero, new Vector2(19, 19), 13, gold, FontStyles.Bold);
            number.outlineColor = new Color32(109, 70, 28, 255);
            number.outlineWidth = .14f;
        }

        private void AttachOfferHover(RectTransform offer, int index) =>
            AttachUnitHover(offer, () => index < State.Offers.Count && State.Offers[index] is UnitRole role ? (role, false) : null);

        private void AttachUnitHover(RectTransform source, Func<(UnitRole Role, bool Upgraded)?> readUnit)
        {
            if (offerHoverCard == null || offerHoverCard.transform.parent != root)
                offerHoverCard = TavernUnitHoverCard.Create(root);
            source.gameObject.AddComponent<TavernOfferHover>().Bind(offerHoverCard, readUnit);
        }

        public void BeginAssault()
        {
            if (standalone || leaving || !State.Ready) return;
            leaving = true;
            FormationReserveBattle.EnterFromTavern(State);
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
            if (OfferIndex >= 0 && !BattlegroundsUpgradeScene.State.CanBuy(OfferIndex)) return;
            group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.alpha=.45f;
            ghost=LegacyUiKit.CreateRect(owner.Canvas.transform,"Dragged Unit",Vector2.zero,new Vector2(145,65));
            var label = OfferIndex >= 0
                ? $"RECRUIT  {TavernRecruitment.RecruitCost(BattlegroundsUpgradeScene.State.Offers[OfferIndex].Value)}"
                : "MOVE UNIT";
            var text=LegacyUiKit.CreateText(ghost,"Label",label,Vector2.zero,new Vector2(145,65),22,Color.yellow,FontStyles.Bold);
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
