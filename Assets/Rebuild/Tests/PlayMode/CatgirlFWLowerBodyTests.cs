#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    public sealed class CatgirlFWLowerBodyTests
    {
        private const string Folder="Assets/Rebuild/CatgirlFrontTest/FWLowerBody";
        [UnityTest]
        public IEnumerator NativeFWLegsKeepCatgirlClothesAndBootsAndAnimateTogether()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Requires rendered-pose verification.");
            Directory.CreateDirectory("Logs/CatgirlFWLower");Application.runInBackground=true;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CatgirlFWLowerBody.prefab"));
            root.GetComponent<UnitView>().enabled=false;root.transform.position=new Vector3(1000,0,1000);
            var cameraObject=new GameObject("Hybrid verification camera");var camera=cameraObject.AddComponent<Camera>();var target=new RenderTexture(512,512,24);
            try
            {
                var driver=root.GetComponent<FWUnitVisual>();var animator=driver.ActiveAnimator;
                Assert.That(root.GetComponentsInChildren<Animator>(true).Length,Is.EqualTo(1));
                Assert.That(animator.applyRootMotion,Is.False);
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                var all=root.GetComponentsInChildren<SpriteRenderer>();var visible=all.Where(s=>s.enabled).ToArray();
                var old=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/CatgirlFrontTest/CatgirlFrontUnitVisual.prefab");
                foreach(var name in new[]{"F_FACE","F_HAIR_FRONT","F_BODY","F_COAT_BACK","F_COAT_SCREEN_L","F_COAT_SCREEN_R","F_SHORTS_CONTINUOUS"})
                    Assert.That(visible.Single(s=>s.name==name && !s.transform.IsChildOf(animator.transform.Find("FW Lower Body"))).sprite,
                        Is.SameAs(old.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name==name).sprite),name+" must retain original Catgirl art.");
                foreach(var side in new[]{"L","R"})
                {
                    var boot=visible.Single(s=>s.name=="Catgirl Original Boot "+side);
                    var original=old.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_BOOT_SCREEN_"+side);
                    Assert.That(boot.sprite.texture,Is.SameAs(original.sprite.texture));
                }
                Assert.That(visible.Any(s=>s.name.StartsWith("F_LEG_CONTINUOUS_")),Is.False);
                var nativeSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/FWNativeLowerBody.asset");
                var native=visible.Single(s=>s.sprite==nativeSprite).GetComponent<SpriteSkin>();
                Assert.That(native.boneTransforms.Any(t=>t.name=="F_F_L_bone_12"),Is.True);
                Assert.That(native.boneTransforms.Any(t=>t.name=="F_F_R_bone_14"),Is.True);
                foreach(var skin in root.GetComponentsInChildren<SpriteSkin>().Where(s=>s.enabled))skin.forceCpuDeformation=true;
                foreach(var state in new[]{"Idle","Run"})
                {
                    var hybrid=animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith(state));
                    var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FW_MIHO/Animation/F_"+(state=="Idle"?"IDLE01":"RUN01")+".anim");
                    foreach(var binding in AnimationUtility.GetCurveBindings(source).Where(b=>b.propertyName=="localEulerAnglesRaw.z" && b.path.Contains("F_F_")))
                    {
                        var copied=binding;copied.path="FW Lower Body/"+binding.path;
                        var a=AnimationUtility.GetEditorCurve(source,binding);var b=AnimationUtility.GetEditorCurve(hybrid,copied);
                        Assert.That(b,Is.Not.Null);
                        foreach(var phase in new[]{0,.25f,.5f,.75f,1})Assert.That(b.Evaluate(phase*hybrid.length),Is.EqualTo(a.Evaluate(phase*source.length)).Within(.01f));
                    }
                    var upper=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Rebuild/CatgirlFrontTest/CatgirlFront"+state+".anim");
                    foreach(var binding in AnimationUtility.GetCurveBindings(upper).Where(b=>!b.path.StartsWith("Skeleton/Pelvis/Thigh") && !(b.path=="Skeleton/Pelvis" && b.propertyName=="m_LocalPosition.y")))
                    {
                        var a=AnimationUtility.GetEditorCurve(upper,binding);var b=AnimationUtility.GetEditorCurve(hybrid,binding);
                        Assert.That(b,Is.Not.Null,binding.path+":"+binding.propertyName);
                        foreach(var phase in new[]{0,.25f,.5f,.75f,1})Assert.That(b.Evaluate(phase*hybrid.length),Is.EqualTo(a.Evaluate(phase*upper.length)).Within(.0001f),"Preserve upper-body edits: "+binding.path);
                    }
                }
                camera.orthographic=true;camera.orthographicSize=.95f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f,1);camera.targetTexture=target;
                camera.transform.position=root.transform.position+new Vector3(0,2.5f,-5);camera.transform.LookAt(root.transform.position+Vector3.up*.7f);
                driver.SetLocomotion(Vector3.right,true,false,camera,0);
                var origin=root.transform.position;Vector3[] first=null;float? waistGap=null;
                var hips=native.boneTransforms.Where(t=>t.name=="F_F_L_bone_11"||t.name=="F_F_R_bone_13").ToArray();
                var ownPelvis=animator.transform.Find("Skeleton/Pelvis");
                for(var sample=0;sample<8;sample++)
                {
                    animator.Play("Run",0,sample/8f);animator.Update(0);yield return null;yield return null;
                    var gap=hips.Average(h=>animator.transform.InverseTransformPoint(h.position).y)-animator.transform.InverseTransformPoint(ownPelvis.position).y;
                    if(!waistGap.HasValue)waistGap=gap;
                    Assert.That(gap,Is.EqualTo(waistGap.Value).Within(.001f),"Waist bob must remain aligned throughout Run.");
                    Capture(camera,target,"HybridRun"+sample);
                    var vertices=native.GetDeformedVertexPositionData().ToArray();
                    if(first==null)first=vertices;
                    if(sample==4)Assert.That(first.Where((v,i)=>Vector3.Distance(v,vertices[i])>.01f).Any(),Is.True,"Native FW leg mesh must actually deform.");
                    Assert.That(driver.FacingBack,Is.False);Assert.That(root.transform.position,Is.EqualTo(origin));
                }
                driver.SetLocomotion(Vector3.zero,false,false,camera);
                yield return new WaitForSeconds(.25f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);Capture(camera,target,"HybridIdle");
                driver.SetLocomotion(Vector3.forward,true,true,camera);
                Assert.That(animator.speed,Is.Zero);Assert.That(driver.IsMoving,Is.False);
                // Isolate only the native legs and original brown boots. Two connected
                // silhouettes (or one while crossing) should account for all rendered pixels.
                foreach(var sprite in visible)sprite.enabled=sprite==native.GetComponent<SpriteRenderer>()||sprite.name.StartsWith("Catgirl Original Boot ");
                camera.backgroundColor=Color.magenta;camera.orthographicSize=.45f;
                camera.transform.position=root.transform.position+new Vector3(0,2.1f,-5);camera.transform.LookAt(root.transform.position+Vector3.up*.2f);
                driver.SetLocomotion(Vector3.right,true,false,camera,0);
                for(var sample=0;sample<8;sample++)
                {
                    animator.Play("Run",0,sample/8f);animator.Update(0);yield return null;yield return null;
                    var pixels=Capture(camera,target,"HybridLowerOnly"+sample);var background=pixels[0];
                    var mask=pixels.Select(c=>Difference(c,background)>30).ToArray();
                    var count=mask.Count(v=>v);Assert.That(count,Is.GreaterThan(1000));
                    var areas=Components(mask,512,512).OrderByDescending(a=>a).ToArray();
                    Assert.That((float)areas.Take(2).Sum()/count,Is.GreaterThan(.995f),"FW legs and Catgirl boots must remain attached, sample "+sample);
                }
            }
            finally{camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(cameraObject);Object.Destroy(root);}
        }
        private static IEnumerable<int> Components(bool[] mask,int width,int height)
        {
            var seen=new bool[mask.Length];var queue=new int[mask.Length];
            for(var i=0;i<mask.Length;i++)
            {
                if(!mask[i]||seen[i])continue;var head=0;var tail=1;queue[0]=i;seen[i]=true;
                while(head<tail)
                {
                    var p=queue[head++];var x=p%width;var y=p/width;
                    for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                    {
                        var nx=x+dx;var ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
                        var n=ny*width+nx;if(!mask[n]||seen[n])continue;seen[n]=true;queue[tail++]=n;
                    }
                }
                yield return tail;
            }
        }
        private static int Difference(Color32 a,Color32 b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        private static Color32[] Capture(Camera camera,RenderTexture target,string name)
        {
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
            try{image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes("Logs/CatgirlFWLower/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            finally{RenderTexture.active=old;Object.Destroy(image);}
        }
    }
}
#endif
