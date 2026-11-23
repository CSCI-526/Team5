#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class BattlegroundsUpgradeTests
    {
        [UnityTest] public IEnumerator PurchaseDragTripleDiscoverAndBattleReturnUseExistingUnits()
        {
            BattlegroundsUpgradeScene.NewGame(31);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BattlegroundsUpgradeScene.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var view=Object.FindFirstObjectByType<BattlegroundsUpgradeScene>();
            var state=BattlegroundsUpgradeScene.State;state.Offers[0]=UnitRole.Attack;
            view.BuyOffer(0);yield return null;
            Assert.AreEqual(0,state.Gold);Assert.AreEqual(1,state.BenchCount);
            var recruit=state.Followers.Single(f=>f.Slot<0);
            var item=view.Canvas.GetComponentsInChildren<TavernDragItem>().Single(d=>d.MemberId==recruit.Id);
            var target=view.Canvas.GetComponentsInChildren<TavernDropTarget>().Single(d=>d.name=="Formation Slot 0");
            var drag=new PointerEventData(EventSystem.current){pointerDrag=item.gameObject,position=new Vector2(400,400)};
            ExecuteEvents.Execute(item.gameObject,drag,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(item.gameObject,drag,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target.gameObject,drag,ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(item.gameObject,drag,ExecuteEvents.endDragHandler);
            Assert.AreEqual(0,recruit.Slot);Assert.AreEqual(3,state.DeployedCount);
            yield return null;Capture(view,"Shop");
            state.Resolve(true);state.Offers[0]=UnitRole.Attack;view.BuyOffer(0);
            yield return null;Assert.AreEqual(3,state.Discover.Count);
            var golden=state.Followers.Single(f=>f.Role==UnitRole.Attack);Assert.IsTrue(golden.Golden);
            Assert.IsFalse(state.Ready);Capture(view,"Discovery");
            var gold=state.Gold;
            view.Canvas.GetComponentsInChildren<Button>().First(b=>b.name=="Choose").onClick.Invoke();
            yield return null;Assert.AreEqual(0,state.Discover.Count);Assert.AreEqual(gold,state.Gold);
            var deployed=state.Deployment().ToArray();
            view.BeginAssault();
            for(var i=0;i<100&&SceneManager.GetActiveScene().path!=SiegeTavernMode.BattleScene;i++)yield return null;
            yield return null;
            var session=Object.FindFirstObjectByType<BattleSession>();Assert.IsNotNull(session?.Simulation);
            for(var i=0;i<100&&session.Simulation.PendingSpawnCount>0;i++)session.Advance(.02f);
            CollectionAssert.AreEquivalent(deployed.Select(m=>m.MemberId),session.Simulation.Units.Select(u=>u.MemberId));
            Assert.AreEqual(state.Stats(golden).MaxHp,session.Simulation.Units.Single(u=>u.Id==golden.Id).MaxHp);
            for(var i=0;i<1500&&session!=null;i++){session.Advance(.1f);yield return null;}
            Assert.AreEqual(BattlegroundsUpgradeScene.ScenePath,SceneManager.GetActiveScene().path);
            Assert.AreSame(state,BattlegroundsUpgradeScene.State);Assert.AreEqual(3,state.Turn);
        }
        private static void Capture(BattlegroundsUpgradeScene view,string name)
        {
            var camera=view.Canvas.worldCamera;var rt=RenderTexture.GetTemporary(1600,900,24);var old=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory("Logs/Battlegrounds");File.WriteAllBytes("Logs/Battlegrounds/"+name+".png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
    }
}
#endif
