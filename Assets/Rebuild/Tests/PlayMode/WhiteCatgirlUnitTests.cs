#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Team5.Rebuild.Tests
{
    public sealed class WhiteCatgirlUnitTests
    {
        [UnityTest]
        public IEnumerator SavedPoseNativeLegDeformationLocomotionPauseAndRendering()
        {
            Directory.CreateDirectory("Logs/WhiteCatgirlUnit");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/WhiteCatgirlUnit/WhiteCatgirlUnitVisual.prefab");
            Assert.That(prefab,Is.Not.Null);
            var approved=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/WhiteCatgirlAssembly/WhiteCatgirl_EditableAssembly.prefab");
            var root=Object.Instantiate(prefab);root.GetComponent<UnitView>().enabled=false;root.transform.position=new Vector3(1000,0,1000);
            var cameraObject=new GameObject("White Catgirl independent render fixture");
            var camera=cameraObject.AddComponent<Camera>();var target=new RenderTexture(640,768,24);
            var previousTimeScale=Time.timeScale;Time.timeScale=1;
            try
            {
                var driver=root.GetComponent<FWUnitVisual>();var animator=driver.ActiveAnimator;
                var rig=animator.transform;
                Assert.That(root.GetComponentsInChildren<Animator>(true).Length,Is.EqualTo(1));
                Assert.That(animator.applyRootMotion,Is.False);Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                foreach(var skin in root.GetComponentsInChildren<SpriteSkin>(true).Where(s=>s.enabled))skin.forceCpuDeformation=true;
                camera.orthographic=true;camera.orthographicSize=.65f;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.15f,.2f,.24f);camera.targetTexture=target;
                camera.transform.position=root.transform.position+new Vector3(0,.56f,-5);camera.transform.rotation=Quaternion.identity;
                driver.SetLocomotion(Vector3.zero,false,false,camera,0);
                animator.Play("Idle",0,0);animator.Update(0);
                yield return null;yield return null;
                var nativeSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Rebuild/WhiteCatgirlUnit/SkinnedSprites/CuteGirlNativeLegs.asset");
                var native=root.GetComponentsInChildren<SpriteSkin>(true).Single(s=>s.enabled&&s.GetComponent<SpriteRenderer>().sprite==nativeSprite);
                Assert.That(native.boneTransforms.Any(b=>b.name=="F_F_L_bone_12"),Is.True);
                Assert.That(native.boneTransforms.Any(b=>b.name=="F_F_R_bone_14"),Is.True);
                var desired=approved.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.enabled&&!s.name.StartsWith("F_LEG_SCREEN_")).ToArray();
                foreach(var part in desired)
                {
                    var actual=rig.Find(part.name).GetComponent<SpriteRenderer>();
                    Assert.That(actual.sprite.texture,Is.SameAs(part.sprite.texture),part.name+" artwork");
                    Assert.That(Vector3.Distance(actual.transform.localPosition,approved.transform.InverseTransformPoint(part.transform.position)),Is.LessThan(.00001f),part.name+" position");
                    Assert.That(Quaternion.Angle(actual.transform.localRotation,Quaternion.Inverse(approved.transform.rotation)*part.transform.rotation),Is.LessThan(.001f),part.name+" rotation");
                    var skin=actual.GetComponent<SpriteSkin>();
                    var deformed=skin.GetDeformedVertexPositionData().ToArray();
                    var rest=actual.sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
                    Assert.That(deformed.Length,Is.EqualTo(rest.Length));
                    // Off-map world coordinates around 1000 incur float cancellation in skin matrices.
                    // Half a source pixel is below the visible threshold at 1000 pixels per unit.
                    for(var i=0;i<rest.Length;i++)Assert.That(Vector3.Distance(deformed[i],rest[i]),Is.LessThan(.0005f),part.name+" must retain approved Idle bind pose");
                }
                var idlePixels=Capture(camera,target,"Idle");
                var origin=root.transform.position;
                Vector3[] first=null;
                driver.SetLocomotion(Vector3.right,true,false,camera,0);
                for(var i=0;i<8;i++)
                {
                    animator.Play("Run",0,i*.125f);animator.Update(0);yield return null;yield return null;
                    var vertices=native.GetDeformedVertexPositionData().ToArray();
                    if(first==null)first=vertices;
                    if(i==1)Assert.That(vertices.Where((p,k)=>Vector3.Distance(p,first[k])>.002f).Any(),Is.True,"Native FW mesh must deform during Run");
                    Capture(camera,target,"Run"+i);
                    Assert.That(root.transform.position,Is.EqualTo(origin));
                }
                driver.SetLocomotion(Vector3.left,true,false,camera,0);Assert.That(driver.Mirrored,Is.True);Assert.That(driver.FacingBack,Is.False);
                driver.SetLocomotion(Vector3.zero,false,false,camera,1);
                yield return new WaitForSeconds(.3f);Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
                driver.SetLocomotion(Vector3.right,true,true,camera);
                Assert.That(driver.IsMoving,Is.False);Assert.That(animator.speed,Is.Zero);
                var time=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSeconds(.08f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(time));
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                {
                    foreach(var r in root.GetComponentsInChildren<SpriteRenderer>(true))r.enabled=r==native.GetComponent<SpriteRenderer>()||r.name.StartsWith("White Catgirl Boot ");
                    driver.SetLocomotion(Vector3.right,true,false,camera,0);
                    for(var phase=0;phase<8;phase++)
                    {
                        animator.Play("Run",0,phase*.125f);animator.Update(0);yield return null;yield return null;
                        var pixels=Capture(camera,target,"LowerRun"+phase);var bg=pixels[0];
                        var mask=pixels.Select(p=>Mathf.Abs(p.r-bg.r)+Mathf.Abs(p.g-bg.g)+Mathf.Abs(p.b-bg.b)>30).ToArray();
                        var areas=Components(mask,target.width,target.height).OrderByDescending(a=>a).ToArray();
                        Assert.That((float)areas.Take(2).Sum()/mask.Count(v=>v),Is.GreaterThan(.99f),"Legs and boots must stay attached at phase "+phase);
                        var pants=rig.Find("F_PELVIS").GetComponent<SpriteRenderer>();pants.enabled=true;
                        var joined=Capture(camera,target,"HipLegBootRun"+phase);var joinedBackground=joined[0];
                        var joinedMask=joined.Select(p=>Mathf.Abs(p.r-joinedBackground.r)+Mathf.Abs(p.g-joinedBackground.g)+Mathf.Abs(p.b-joinedBackground.b)>30).ToArray();
                        var joinedAreas=Components(joinedMask,target.width,target.height).OrderByDescending(a=>a).ToArray();
                        Assert.That((float)joinedAreas.First()/joinedMask.Count(v=>v),Is.GreaterThan(.99f),"Shorts, both thighs and boots must form one attached silhouette at phase "+phase);
                        pants.enabled=false;
                    }
                }
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                {
                    var background=idlePixels[0];
                    Assert.That(idlePixels.Count(p=>Mathf.Abs(p.r-background.r)+Mathf.Abs(p.g-background.g)+Mathf.Abs(p.b-background.b)>30),Is.GreaterThan(2000),"Character visibly rendered");
                }
                File.WriteAllText("Logs/WhiteCatgirlUnit/PlayModeVerified.txt","PASS: approved upper-body Idle vertices, native FW leg deformation, eight Run phases and leg/boot connectivity, root position, mirroring, Idle transition, pause. Graphics: "+SystemInfo.graphicsDeviceType);
            }
            finally{Time.timeScale=previousTimeScale;camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(cameraObject);Object.Destroy(root);}
        }
        private static Color32[] Capture(Camera camera,RenderTexture target,string name)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return new Color32[0];
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try{texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes("Logs/WhiteCatgirlUnit/"+name+".png",texture.EncodeToPNG());return texture.GetPixels32();}
            finally{RenderTexture.active=previous;Object.Destroy(texture);}
        }
        private static System.Collections.Generic.IEnumerable<int> Components(bool[] mask,int width,int height)
        {
            var seen=new bool[mask.Length];var queue=new int[mask.Length];
            for(var start=0;start<mask.Length;start++)
            {
                if(!mask[start]||seen[start])continue;var head=0;var tail=1;queue[0]=start;seen[start]=true;
                while(head<tail){var p=queue[head++];var x=p%width;var y=p/width;for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++){var nx=x+dx;var ny=y+dy;if(nx<0||ny<0||nx>=width||ny>=height)continue;var n=ny*width+nx;if(!seen[n]&&mask[n]){seen[n]=true;queue[tail++]=n;}}}
                yield return tail;
            }
        }
    }
}
#endif
