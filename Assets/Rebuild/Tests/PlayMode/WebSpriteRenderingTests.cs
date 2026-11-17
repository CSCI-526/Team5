#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;

namespace Team5.Rebuild.Tests
{
    public sealed class WebSpriteRenderingTests
    {
        [UnityTest] public IEnumerator MobileWorldZSpritesNeedDepthAndRenderWithCpuDeformation()
        {
            Application.runInBackground=true;Directory.CreateDirectory("Logs/WebSpriteRendering");
            var utility=typeof(SpriteSkin).Assembly.GetType("UnityEngine.U2D.Animation.SpriteSkinUtility");
            var force=utility.GetMethod("SetUsingGpuDeformationForTest",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That(force,Is.Not.Null,"Require the package test override to exercise WebGL-style CPU deformation.");
            force.Invoke(null,new object[]{(bool?)false});
            var source=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(source.supportsCameraDepthTexture,Is.True,"The actual Web/Mobile pipeline must provide the world-Z shader's depth texture.");
            var previousPipeline=QualitySettings.renderPipeline;var mobile=Object.Instantiate(source);
            mobile.supportsCameraDepthTexture=false;QualitySettings.renderPipeline=mobile;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlCaptain.prefab"));
            var cameraObject=new GameObject("Web Sprite CPU verification");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(512,512,24);Material control=null;
            try
            {
                root.GetComponent<UnitView>().enabled=false;root.transform.position=new Vector3(1000,0,1000);
                camera.transform.position=root.transform.position+new Vector3(0,2.5f,-5);camera.transform.LookAt(root.transform.position+Vector3.up*.75f);
                camera.orthographic=true;camera.orthographicSize=1.25f;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.03f,.07f,.11f,1);camera.targetTexture=target;
                var driver=root.GetComponent<FWUnitVisual>();
                for(var i=0;i<5;i++){driver.SetLocomotion(Vector3.right,true,false,camera);yield return null;}
                var sprites=root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).ToArray();
                Assert.That(sprites.Length,Is.GreaterThan(10));
                var materials=sprites.Select(s=>s.sharedMaterial).ToArray();
                Shader.SetGlobalTexture("_CameraDepthTexture",Texture2D.blackTexture);
                var missing=Capture(camera,target,"WorldZ_MissingDepth");
                mobile.supportsCameraDepthTexture=true;
                yield return null;yield return null;
                var first=Capture(camera,target,"WorldZ_CPU");
                control=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                foreach(var sprite in sprites)sprite.sharedMaterial=control;
                yield return null;yield return null;
                var reference=Capture(camera,target,"Default_CPU");
                File.WriteAllText("Logs/WebSpriteRendering/Comparison.txt",$"Graphics: {SystemInfo.graphicsDeviceType}; missing-depth pixels: {missing}; depth-enabled worldZ pixels: {first}; default pixels: {reference}; sprites: {sprites.Length}");
                Assert.That(reference,Is.GreaterThan(1000),"Control sprite shader must render CPU-deformed geometry.");
                Assert.That(missing,Is.LessThan(reference*.1f),"Reproduce invisible sprites with the old Mobile depth setting and missing black depth texture.");
                Assert.That(first,Is.GreaterThan(reference*.4f),"World-Z shader must not discard the character on the OpenGL/CPU path.");
            }
            finally
            {
                force.Invoke(null,new object[]{null});camera.targetTexture=null;Object.Destroy(root);Object.Destroy(cameraObject);
                QualitySettings.renderPipeline=previousPipeline;Object.Destroy(mobile);
                if(control!=null)Object.Destroy(control);target.Release();Object.Destroy(target);
            }
        }
        private static int Capture(Camera camera,RenderTexture target,string name)
        {
            var previous=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes("Logs/WebSpriteRendering/"+name+".png",texture.EncodeToPNG());
                var pixels=texture.GetPixels32();var background=pixels[0];
                return pixels.Count(c=>Mathf.Abs(c.r-background.r)+Mathf.Abs(c.g-background.g)+Mathf.Abs(c.b-background.b)>45);
            }
            finally{RenderTexture.active=previous;Object.Destroy(texture);}
        }
    }
}
#endif
