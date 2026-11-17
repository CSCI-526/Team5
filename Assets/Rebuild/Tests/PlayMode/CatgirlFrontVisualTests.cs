#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Team5.Rebuild.Tests
{
    public sealed class CatgirlFrontVisualTests
    {
        private const string Folder = "Assets/Rebuild/CatgirlFrontTest";
        private const string ScenePath = "Assets/Rebuild/Scenes/CatgirlFrontTest.unity";
        private Scene initialScene;
        [SetUp] public void Setup()
        {
            initialScene=SceneManager.GetActiveScene();
            Directory.CreateDirectory("Logs/CatgirlFront"); Application.runInBackground=true;
        }

        [UnityTest]
        public IEnumerator NativeFrontSkinChangesRenderedPoseWithoutMovingRootAndPausesInIdle()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) Assert.Ignore("Requires graphics for rendered-pose verification.");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CatgirlFrontUnitVisual.prefab"));
            root.GetComponent<UnitView>().enabled=false;
            root.transform.position=new Vector3(1000,0,1000);
            var cameraObject=new GameObject("Catgirl isolated verification camera");
            var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(512,512,24);
            try
            {
                var driver=root.GetComponent<FWUnitVisual>();
                var animator=root.GetComponentInChildren<Animator>();
                var skins=root.GetComponentsInChildren<SpriteSkin>();
                Assert.That(skins.Length,Is.EqualTo(20));
                Assert.That(root.GetComponentsInChildren<Animator>(true).Length,Is.EqualTo(1));
                Assert.That(animator.applyRootMotion,Is.False);
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider2D>(true),Is.Empty);
                foreach(var skin in skins)
                {
                    Assert.That(skin.rootBone,Is.Not.Null);
                    var count=skin.name.StartsWith("F_LEG_CONTINUOUS_")?3:1;
                    Assert.That(skin.boneTransforms.Length,Is.EqualTo(count));
                    var sprite=skin.GetComponent<SpriteRenderer>().sprite;
                    Assert.That(sprite.GetBones().Length,Is.EqualTo(count));
                    Assert.That(sprite.GetBindPoses().Length,Is.EqualTo(count));
                    if(count==3)
                    {
                        var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                        Assert.That(sprite.GetVertexCount(),Is.GreaterThan(100));
                        Assert.That(weights.Any(w=>w.weight0>0 && w.weight1>0),Is.True,"Knee vertices need mixed thigh/shin influence.");
                        Assert.That(weights.Any(w=>w.weight1>0 && w.weight2>0),Is.True,"Ankle vertices need mixed shin/boot influence.");
                        foreach(var w in weights)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001f));
                    }
                    skin.forceCpuDeformation=true;
                }
                camera.transform.position=root.transform.position+new Vector3(0,2.5f,-5);
                camera.transform.LookAt(root.transform.position+Vector3.up*.7f);
                camera.orthographic=true;camera.orthographicSize=.95f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f,1);camera.targetTexture=target;
                var origin=root.transform.position;
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                {
                    driver.SetLocomotion(direction,true,false,camera,0);
                    Assert.That(driver.FacingBack,Is.False);Assert.That(driver.IsMoving,Is.True);
                }
                // A moving hand alone can be caused by a shoulder swing with a straight elbow.
                // Measure actual upper/lower limb directions throughout the complete run cycle.
                var joints=animator.GetComponentsInChildren<Transform>();
                for(var sample=0;sample<=8;sample++)
                {
                    animator.Play("Run",0,sample/8f);animator.Update(0);
                    foreach(var side in new[]{"L","R"})
                    {
                        var shoulder=joints.Single(t=>t.name=="Arm"+side);
                        var elbow=joints.Single(t=>t.name=="Forearm"+side);
                        var wrist=joints.Single(t=>t.name=="Hand"+side);
                        var bend=Vector3.Angle(elbow.position-shoulder.position,wrist.position-elbow.position);
                        Assert.That(bend,Is.InRange(45f,140f),"Elbow "+side+" must visibly fold throughout Run, phase "+sample/8f);
                    }
                }
                // Validate actual knee flexion and half-cycle alternation, not merely leg movement.
                foreach(var phase in new[]{.25f,.75f})
                {
                    animator.Play("Run",0,phase);animator.Update(0);
                    foreach(var side in new[]{"L","R"})
                    {
                        var hip=joints.Single(t=>t.name=="Thigh"+side);
                        var knee=joints.Single(t=>t.name=="Shin"+side);
                        var ankle=joints.Single(t=>t.name=="Boot"+side);
                        var bend=Vector3.Angle(knee.position-hip.position,ankle.position-knee.position);
                        var recovery=(phase==.25f && side=="R")||(phase==.75f && side=="L");
                        Assert.That(bend,recovery?Is.InRange(50f,80f):Is.LessThan(20f),"Knee "+side+" flexion at phase "+phase);
                    }
                }
                var first=joints.Where(t=>t.name=="ThighL"||t.name=="ShinL"||t.name=="BootL"||t.name=="ThighR"||t.name=="ShinR"||t.name=="BootR").ToArray();
                animator.Play("Run",0,0);animator.Update(0);
                var start=first.Select(t=>(t.localPosition,t.localRotation)).ToArray();
                animator.Play("Run",0,.99999f);animator.Update(0);
                for(var i=0;i<first.Length;i++)
                {
                    Assert.That(Vector3.Distance(start[i].localPosition,first[i].localPosition),Is.LessThan(.001f));
                    Assert.That(Quaternion.Angle(start[i].localRotation,first[i].localRotation),Is.LessThan(.1f));
                }
                // Native deformation and final rendered pixels must BOTH move, not just Animator states.
                var leg=skins.Single(s=>s.name=="F_LEG_CONTINUOUS_SCREEN_L");
                animator.Play("Run",0,.15f);animator.Update(0);
                yield return null;yield return null;
                var vertices=leg.GetDeformedVertexPositionData().ToArray();
                var hands=skins.Where(s=>s.name=="F_HAND_SCREEN_L" || s.name=="F_HAND_SCREEN_R").ToArray();
                var handVertices=hands.Select(s=>s.GetDeformedVertexPositionData().ToArray()).ToArray();
                var a=Capture(camera,target,"RunA");
                animator.Play("Run",0,.65f);animator.Update(0);
                yield return null;yield return null;
                var next=leg.GetDeformedVertexPositionData().ToArray();
                Assert.That(vertices.Where((v,i)=>Vector3.Distance(v,next[i])>.001f).Any(),Is.True,"SpriteSkin vertices must deform with leg motion.");
                for(var i=0;i<hands.Length;i++)
                {
                    var moved=hands[i].GetDeformedVertexPositionData().ToArray();
                    var distance=handVertices[i].Select((v,j)=>Vector3.Distance(v,moved[j])).Average();
                    Assert.That(distance,Is.GreaterThan(.035f),hands[i].name+" must visibly swing with the shoulder/elbow chain.");
                }
                var b=Capture(camera,target,"RunB");
                Assert.That(a.Where((c,i)=>Difference(c,b[i])>24).Count(),Is.GreaterThan(150),"Final sprite render must visibly animate.");
                driver.SetLocomotion(Vector3.zero,false,false,camera,1);
                yield return new WaitForSeconds(.25f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
                var head=animator.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
                var headRotation=head.localRotation;
                yield return new WaitForSeconds(.3f);
                Assert.That(Quaternion.Angle(headRotation,head.localRotation),Is.GreaterThan(.01f),"Idle should have subtle bone animation.");
                Capture(camera,target,"Idle");
                driver.SetLocomotion(Vector3.forward,true,true,camera);
                Assert.That(driver.IsMoving,Is.False);
                Assert.That(animator.speed,Is.Zero);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
                headRotation=head.localRotation;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Quaternion.Angle(headRotation,head.localRotation),Is.LessThan(.001f));
                Assert.That(root.transform.position,Is.EqualTo(origin));
                foreach(var direction in new[]{Vector3.left,Vector3.right})
                {
                    driver.SetLocomotion(direction,true,false,camera,0);
                    Assert.That(driver.Mirrored,Is.EqualTo(direction==Vector3.left));
                    Assert.That(driver.FacingBack,Is.False);
                }
                var psbSprites=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Source/CatgirlFront.psb").OfType<Sprite>().Count();
                Assert.That(psbSprites,Is.GreaterThanOrEqualTo(24),"PSD Importer should import individual front layers.");
                File.WriteAllText("Logs/CatgirlFront/Verified.txt","Native skins: 20, including 2 continuous three-bone leg meshes; imported original PSB sprites: "+psbSprites+"; Idle/Run bones, CPU deformation and rendered-pixel movement verified; front-only, mirror, pause and fixed root verified.");
            }
            finally
            {
                camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(root);Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator TestSceneReplacesOnlyMember111AndFollowsSimulationLocomotion()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            var presentation=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var sim=presentation.Session.Simulation;
            var units=sim.Units.ToArray();
            var unit=units.Single(u=>u.MemberId==111);
            Assert.That(presentation.TryGetView(unit.Id,out var view),Is.True);
            var driver=view.GetComponent<FWUnitVisual>();
            foreach(var other in units.Where(u=>u.MemberId!=111))
            {
                Assert.That(presentation.TryGetView(other.Id,out var otherView),Is.True);
                Assert.That(otherView.GetComponentInChildren<Animator>().runtimeAnimatorController.name,Does.StartWith("FW"));
            }
            Assert.That(driver.ActiveAnimator.runtimeAnimatorController.name,Is.EqualTo("CatgirlFWLowerBody"));
            Assert.That(sim.RequestMovement(new MovementRequest(sim.SquadReferencePosition+Vector3.back*3)),Is.True);
            var initial=unit.Position;
            var deadline=Time.unscaledTime+5;
            while(Time.unscaledTime<deadline && !driver.IsMoving)yield return null;
            Assert.That(driver.IsMoving,Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector3.Distance(initial,unit.Position),Is.GreaterThan(.01f));
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run"),Is.True);
            Assert.That(driver.FacingBack,Is.False);
            sim.SetPaused(true);yield return null;yield return null;
            Assert.That(driver.IsMoving,Is.False);Assert.That(driver.ActiveAnimator.speed,Is.Zero);
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
        }
        [UnityTest]
        public IEnumerator ContinuousLegsRenderAsOneConnectedSilhouetteThroughoutRun()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Requires graphics for silhouette checks.");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CatgirlFrontUnitVisual.prefab"));
            root.GetComponent<UnitView>().enabled=false;root.transform.position=new Vector3(1000,0,1000);
            var cameraObject=new GameObject("Continuous leg verification camera");
            var camera=cameraObject.AddComponent<Camera>();var target=new RenderTexture(512,512,24);
            try
            {
                var driver=root.GetComponent<FWUnitVisual>();var animator=driver.ActiveAnimator;
                var sprites=root.GetComponentsInChildren<SpriteRenderer>();
                var legs=sprites.Where(s=>s.name.StartsWith("F_LEG_CONTINUOUS_")).ToArray();
                Assert.That(legs.Length,Is.EqualTo(2));
                foreach(var sprite in sprites)sprite.enabled=false;
                camera.orthographic=true;camera.orthographicSize=.35f;camera.targetTexture=target;
                // Use a color absent from the leg artwork: the dark boot cuff can match a
                // charcoal background and otherwise be incorrectly counted as a real gap.
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;
                foreach(var leg in legs)
                {
                    leg.enabled=true;
                    camera.transform.position=leg.transform.position+new Vector3(0,2.5f,-5);
                    camera.transform.LookAt(leg.transform.position);
                    driver.SetLocomotion(Vector3.right,true,false,camera,0);
                    camera.transform.position=leg.transform.position+new Vector3(0,2.5f,-5);
                    camera.transform.LookAt(leg.transform.position);
                    for(var sample=0;sample<=8;sample++)
                    {
                        animator.Play("Run",0,sample/8f);animator.Update(0);
                        yield return null;yield return null;
                        var pixels=Capture(camera,target,leg.name+"_Phase"+sample);
                        var mask=pixels.Select(c=>Difference(c,pixels[0])>30).ToArray();
                        var visible=mask.Count(v=>v);Assert.That(visible,Is.GreaterThan(500));
                        var largest=LargestComponent(mask,target.width,target.height);
                        Assert.That((float)largest/visible,Is.GreaterThan(.995f),leg.name+" should be one connected silhouette at phase "+sample/8f);
                    }
                    leg.enabled=false;
                }
            }
            finally{camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(root);Object.Destroy(cameraObject);}
        }
        private static int LargestComponent(bool[] mask,int width,int height)
        {
            var visited=new bool[mask.Length];var queue=new int[mask.Length];var largest=0;
            for(var i=0;i<mask.Length;i++)
            {
                if(!mask[i]||visited[i])continue;
                var head=0;var tail=1;queue[0]=i;visited[i]=true;
                while(head<tail)
                {
                    var p=queue[head++];var x=p%width;var y=p/width;
                    for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                    {
                        var nx=x+dx;var ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
                        var n=ny*width+nx;if(!mask[n]||visited[n])continue;
                        visited[n]=true;queue[tail++]=n;
                    }
                }
                largest=Mathf.Max(largest,tail);
            }
            return largest;
        }
        private static int Difference(Color32 a,Color32 b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        private static Color32[] Capture(Camera camera,RenderTexture target,string name)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                File.WriteAllBytes("Logs/CatgirlFront/"+name+".png",image.EncodeToPNG());return image.GetPixels32();
            }
            finally{RenderTexture.active=previous;Object.Destroy(image);}
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            if(initialScene.IsValid()&&initialScene.isLoaded)SceneManager.SetActiveScene(initialScene);
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
