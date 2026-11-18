using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using Team5.Rebuild.Presentation.LegacyUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation
{
    public sealed partial class SiegeRamFleet
    {
        [Header("Optional support start and gate introduction")]
        [SerializeField] private BattleCell supportSpawnCenter;
        [SerializeField, Tooltip("Hide support slots until the squad has actually stopped against a wooden gate.")]
        private bool unlockAfterWoodenGateStop;
        private bool gateUnlocked;
        private float gateStoppedTime;
        private GameObject gateGuidePanel;
        private float gateIconFlashStart;
        private bool gateIconFlashing;
        private float gateGuideDismissAt=float.PositiveInfinity;
        private readonly Dictionary<int,Vector3> guidePositions=new Dictionary<int,Vector3>();
        public bool IsUnlocked=>!unlockAfterWoodenGateStop||gateUnlocked;

        private bool IsSupportSpawn(BattleCell cell)
        {
            if(cell.Kind!=CellKind.Spawn)return false;
            if(supportSpawnCenter==null)return true; // Existing Scene 1 behaviour.
            var center=supportSpawnCenter.Occupancy;var key=cell.Occupancy;
            return key.y==center.y&&Mathf.Abs(key.x-center.x)<=1&&Mathf.Abs(key.z-center.z)<=1;
        }
        private void ResetGateGuide()
        {
            ClearGateGuide();
            gateUnlocked=false;gateStoppedTime=0;guidePositions.Clear();
            if(toast!=null)toast.text="";toastUntil=0;
        }
        private void UpdateGateGuide(float delta)
        {
            if(gateGuidePanel!=null&&Time.unscaledTime>=gateGuideDismissAt)ClearGateGuide();
            if(!unlockAfterWoodenGateStop||gateUnlocked)return;
            if(simulation.IsPaused||simulation.Outcome!=BattleOutcome.Running)
            {gateStoppedTime=0;return;}
            // Update may run several times between FixedUpdate ticks. No new simulation
            // sample is not evidence of movement and must not erase accumulated stop time.
            if(delta<=0)return;
            var stopped=guidePositions.Count>0;var touchesGate=false;var alive=0;
            foreach(var unit in simulation.Units)
            {
                if(!unit.IsAlive)continue;
                alive++;
                if(!guidePositions.TryGetValue(unit.Id,out var before)||(unit.Position-before).sqrMagnitude>.000001f)stopped=false;
                guidePositions[unit.Id]=unit.Position;
                if(unit.NextWaypoint>=session.Route.Cells.Count)continue;
                var direction=session.Route.Cells[unit.NextWaypoint].TopCenter+unit.FormationOffset-unit.Position;
                foreach(var gate in simulation.Towers)
                {
                    if(!gate.IsAlive||!gate.Definition.IsWoodenGate||Mathf.Abs(gate.Position.y-unit.Position.y)>.1f)continue;
                    var offset=gate.Position-unit.Position;var size=gate.Definition.Footprint;
                    // Match the fixed-route wall clearance, including the 0.001 stop margin.
                    if(Vector3.Dot(direction,offset)>0&&Mathf.Abs(offset.x)<=size.x*.5f+.46f&&
                        Mathf.Abs(offset.z)<=size.y*.5f+.46f)touchesGate=true;
                }
            }
            if(simulation.PendingSpawnCount>0||alive==0||!stopped||!touchesGate)
            {gateStoppedTime=0;return;}
            gateStoppedTime+=delta;
            if(gateStoppedTime<.4f)return;
            gateUnlocked=true;RefreshQuantity();
            ShowGateGuide();
        }
        private void ShowGateGuide(bool replanReminder=false)
        {
            if(hud==null||gateGuidePanel!=null)return;
            var scaler=hud.GetComponent<CanvasScaler>();
            var scale=scaler!=null&&scaler.uiScaleMode==CanvasScaler.ScaleMode.ScaleWithScreenSize?scaler.referenceResolution.x/1280f:1f;
            var panel=LegacyUiKit.CreateImage(hud.transform,"Wooden Gate Guide",Vector2.zero,new Vector2(940,90)*scale,LegacyUiKit.Gold,24*scale);
            gateGuidePanel=panel.gameObject;
            gateIconFlashStart=Time.unscaledTime;
            gateIconFlashing=!replanReminder;
            gateGuideDismissAt=replanReminder?Time.unscaledTime+8:float.PositiveInfinity;
            var rect=panel.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,24)*scale;
            var background=LegacyUiKit.CreateImage(rect,"Background",Vector2.zero,Vector2.zero,new Color(LegacyUiKit.Slate.r,LegacyUiKit.Slate.g,LegacyUiKit.Slate.b,.96f),21*scale);
            LegacyUiKit.Stretch(background.rectTransform);background.rectTransform.offsetMin=Vector2.one*3*scale;background.rectTransform.offsetMax=-Vector2.one*3*scale;
            var label=LegacyUiKit.CreateText(rect,"Gate Instructions",
                replanReminder
                    ? "<b>NEED A LONGER PATH?</b>\nIf your path doesn't reach the wooden gate, click the siege ram to draw a new one."
                    : "<b>WOODEN GATE BLOCKS THE WAY</b>\nClick the siege ram icon, then draw a route from the red start through the gate.",
                Vector2.zero,Vector2.zero,18*scale,Color.white);
            LegacyUiKit.Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(16,8)*scale;label.rectTransform.offsetMax=-new Vector2(16,8)*scale;
            label.textWrappingMode=TextWrappingModes.Normal;
        }
        private void ClearGateGuide()
        {
            gateIconFlashing=false;
            if(gateGuidePanel==null)return;
            gateGuidePanel.SetActive(false);Destroy(gateGuidePanel);gateGuidePanel=null;
            if(ramIcon!=null)ramIcon.color=remainingQuantity>0?Color.white:new Color(.25f,.25f,.25f,1);
        }
        private Color RamIconColor()
        {
            if(remainingQuantity<=0)return new Color(.25f,.25f,.25f,1);
            if(!gateIconFlashing||gateGuidePanel==null||IsPlanning||simulation==null||simulation.Outcome!=BattleOutcome.Running)return Color.white;
            // Gentle one-second pulse; keep the icon visible and keep flashing while paused.
            var alpha=Mathf.Lerp(.35f,1f,.5f+.5f*Mathf.Cos((Time.unscaledTime-gateIconFlashStart)*Mathf.PI*2));
            return new Color(1,1,1,alpha);
        }
    }
}
