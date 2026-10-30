using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Loads the actual upcoming Scene temporarily, without simulation or battle UI.
    public sealed class LegacyNextLevelPreview : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private LegacyUpgradeScene owner;
        private string path;
        private Scene scene;
        private AsyncOperation load;
        private RawImage image;
        private TMP_Text status;
        private RenderTexture texture;
        private Camera previewCamera;
        private bool closed, loadRunning = true, finishing;
        public bool IsReady { get; private set; }
        public Scene LoadedScene => scene;

        public static LegacyNextLevelPreview Open(LegacyUpgradeScene owner)
        {
            if (!Application.CanStreamedLevelBeLoaded(BattleSceneFlow.NextBattleScenePath) || !BattleSceneFlow.TryBeginLevelPreview()) return null;
            var host = new GameObject("Next Level Preview", typeof(RectTransform));
            var view = host.AddComponent<LegacyNextLevelPreview>();
            view.owner = owner; view.path = BattleSceneFlow.NextBattleScenePath;
            view.Build(); view.StartCoroutine(view.LoadMap());
            return view;
        }

        private void Build()
        {
            LegacyUiKit.CreateCanvas(gameObject, 500, new Vector2(1280,720),.5f);
            var background = LegacyUiKit.CreateImage(transform,"Background",Vector2.zero,Vector2.zero,LegacyUiKit.CellColor);
            LegacyUiKit.Stretch(background.rectTransform); background.raycastTarget = true;
            var map = new GameObject("Map",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            map.transform.SetParent(transform,false); image = map.GetComponent<RawImage>();
            LegacyUiKit.Stretch(image.rectTransform); image.raycastTarget = false; image.enabled = false;
            status = LegacyUiKit.CreateText(transform,"Status","Loading map...",Vector2.zero,new Vector2(1000,60),26,Color.white);
            var title = LegacyUiKit.CreateText(transform,"Title",$"LEVEL {BattleSceneFlow.NextBattleLevelNumber} PREVIEW",Vector2.zero,new Vector2(700,50),26,Color.white,FontStyles.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f,1);
            title.rectTransform.pivot = new Vector2(.5f,1); title.rectTransform.anchoredPosition = new Vector2(0,-18);
            var close = LegacyUiKit.CreateButton(transform,"Close",Vector2.zero,new Vector2(52,48),LegacyUiKit.PanelLineColor,6);
            var rect = (RectTransform)close.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-20,-18);
            LegacyUiKit.CreateText(close.transform,"Label","X",Vector2.zero,new Vector2(48,44),27,Color.white,FontStyles.Bold);
            close.onClick.AddListener(Close);
        }

        private IEnumerator LoadMap()
        {
            try { load = SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive); }
            catch (System.Exception error) { status.text = "Unable to load map."; Debug.LogException(error,this); }
            if (load == null) { loadRunning = false; if(closed) yield return Finish(); yield break; }
            yield return load;
            scene = SceneManager.GetSceneByPath(path);
            if (closed) { loadRunning = false; yield return Finish(); yield break; }
            var prepared = false;
            try { prepared = PrepareMap(); }
            catch(System.Exception error) { Debug.LogException(error,this); }
            if (!prepared) { loadRunning = false; status.text = "Unable to display map."; yield break; }
            yield return null; // Let the camera render once before displaying its texture.
            loadRunning = false;
            if (closed) { yield return Finish(); yield break; }
            image.texture = texture; image.enabled = true; status.gameObject.SetActive(false); IsReady = true;
        }

        private bool PrepareMap()
        {
            var bounds = new Bounds(); var found = false;
            var rotation = Quaternion.Euler(55,0,0);
            var towers = new List<BattleTower>();
            foreach(var root in scene.GetRootGameObjects())
            {
                towers.AddRange(root.GetComponentsInChildren<BattleTower>(true));
                foreach(var camera in root.GetComponentsInChildren<Camera>(true)) { rotation = camera.transform.rotation; camera.enabled = false; }
                foreach(var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                foreach(var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach(var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if(!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
                }
            }
            if(!found) return false;
            var towerId = 0;
            foreach(var tower in towers)
            {
                if(!tower.gameObject.activeInHierarchy) continue;
                var rangeObject = new GameObject("Preview Tower Range " + ++towerId);
                SceneManager.MoveGameObjectToScene(rangeObject,scene);
                var range = rangeObject.AddComponent<TowerRangeView>();
                range.BindPreview(tower.CaptureDefinition(towerId));
                foreach(var child in rangeObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
                bounds.Encapsulate(range.DisplayBounds);
            }
            var height = Mathf.Clamp(Screen.height,360,1080);
            var width = Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1,Screen.height));
            texture = new RenderTexture(width,height,24) { name = "Level Preview", antiAliasing = 4 };
            texture.Create();
            var cameraObject = new GameObject("Preview Camera"); SceneManager.MoveGameObjectToScene(cameraObject,scene);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.targetTexture = texture; previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = LegacyUiKit.CellColor;
            previewCamera.orthographic = true; previewCamera.aspect = (float)width/height;
            var extent = Vector3.zero;
            for(var x=-1;x<=1;x+=2) for(var y=-1;y<=1;y+=2) for(var z=-1;z<=1;z+=2)
            {
                var p = Quaternion.Inverse(rotation)*Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                extent = Vector3.Max(extent,new Vector3(Mathf.Abs(p.x),Mathf.Abs(p.y),Mathf.Abs(p.z)));
            }
            previewCamera.orthographicSize = Mathf.Max(1,extent.y,extent.x/previewCamera.aspect)*1.2f;
            previewCamera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*(extent.z+50),rotation);
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = extent.z*2+100;
            var lightObject = new GameObject("Preview Light"); SceneManager.MoveGameObjectToScene(lightObject,scene);
            var lightSource = lightObject.AddComponent<Light>(); lightSource.type = LightType.Directional;
            lightSource.intensity = 1.2f; lightSource.cullingMask = 1 << PreviewLayer; lightSource.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(50,-30,0);
            return true;
        }

        public void Close()
        {
            if(closed) return;
            closed = true; IsReady = false;
            if(!loadRunning) StartCoroutine(Finish());
        }
        private IEnumerator Finish()
        {
            if(finishing) yield break;
            finishing = true; image.enabled = false; status.gameObject.SetActive(true); status.text = "Closing preview...";
            if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            BattleSceneFlow.EndLevelPreview(path);
            if(owner != null) owner.OnNextLevelPreviewClosed();
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            if(previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = null; }
            if(texture != null) { texture.Release(); Destroy(texture); }
            if(scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            if(load != null && !load.isDone)
            {
                var pendingPath = path;
                UnityEngine.Events.UnityAction<Scene,LoadSceneMode> cleanup = null;
                cleanup = (loaded,mode) =>
                {
                    if(loaded.path != pendingPath) return;
                    SceneManager.sceneLoaded -= cleanup;
                    foreach(var root in loaded.GetRootGameObjects()) root.SetActive(false);
                    SceneManager.UnloadSceneAsync(loaded);
                    BattleSceneFlow.EndLevelPreview(pendingPath);
                };
                SceneManager.sceneLoaded += cleanup;
            }
            else BattleSceneFlow.EndLevelPreview(path);
        }
    }
}
