using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Session;
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
        private const float Left = -536f;
        private static readonly Vector2 CardSize = new Vector2(1136f, 652f);
        private static readonly Vector2 MapCenter = new Vector2(0f, 4f);
        private static readonly Vector2 MapSize = new Vector2(1072f, 480f);
        private static readonly Quaternion BoardRotation = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Color DimColor = new Color(0.024f, 0.035f, 0.06f, 0.74f);
        private static readonly Color StartInk = new Color(0.196f, 0.196f, 0.196f);

        private readonly List<TowerRangeView> ranges = new List<TowerRangeView>();
        private readonly List<Camera> strippedCameras = new List<Camera>();
        private LegacyUpgradeScene owner;
        private string path;
        private Scene scene;
        private AsyncOperation load;
        private RectTransform overlayRoot;
        private RawImage image;
        private TMP_Text status;
        private TMP_Text facts;
        private Button startButton;
        private RenderTexture texture;
        private Camera previewCamera;
        private NextMapData data;
        private bool closed, loadRunning = true, finishing, ended, neutralized;

        public bool IsReady { get; private set; }
        public Scene LoadedScene => scene;

        public static LegacyNextLevelPreview Open(LegacyUpgradeScene owner)
        {
            if (!Application.CanStreamedLevelBeLoaded(BattleSceneFlow.NextBattleScenePath) || !BattleSceneFlow.TryBeginLevelPreview()) return null;
            var host = new GameObject("Next Level Preview", typeof(RectTransform));
            var view = host.AddComponent<LegacyNextLevelPreview>();
            view.owner = owner;
            view.path = BattleSceneFlow.NextBattleScenePath;
            view.StripPreviewLayer();
            view.Build();
            SceneManager.sceneLoaded += view.OnPreviewSceneLoaded;
            view.StartCoroutine(view.LoadMap());
            return view;
        }

        private void Update()
        {
            if (startButton != null) startButton.interactable = !closed && owner != null && owner.CanStartWave;
        }

        private void Build()
        {
            LegacyUiKit.CreateCanvas(gameObject, 500, new Vector2(1280f, 720f), 0f);
            var dim = LegacyUiKit.CreateImage(transform, "Dim", Vector2.zero, Vector2.zero, DimColor);
            LegacyUiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            LegacyUiKit.CreateImage(transform, "Card Frame", Vector2.zero, CardSize + new Vector2(4f, 4f), LegacyUiKit.PanelLineColor, 17);
            var card = LegacyUiKit.CreateImage(transform, "Card", Vector2.zero, CardSize, LegacyUiKit.PanelColor, 16).rectTransform;
            BuildHeader(card);
            LabeledButton(card, "Close", "X", new Vector2(514f, 282f), new Vector2(44f, 40f), LegacyUiKit.PanelLineColor, Color.white, 22, true)
                .onClick.AddListener(Close);
            LegacyUiKit.CreateImage(card, "Map Frame", MapCenter, MapSize + new Vector2(3f, 3f), LegacyUiKit.PanelLineColor, 11);
            var panel = LegacyUiKit.CreateImage(card, "Map Panel", MapCenter, MapSize, LegacyUiKit.CellColor, 10);
            panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var map = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            map.transform.SetParent(panel.transform, false);
            image = map.GetComponent<RawImage>();
            LegacyUiKit.Stretch(image.rectTransform);
            image.raycastTarget = false;
            image.enabled = false;
            overlayRoot = LegacyUiKit.CreateRect(panel.transform, "Overlay", Vector2.zero, MapSize);
            overlayRoot.gameObject.SetActive(false);
            status = LegacyUiKit.CreateText(panel.transform, "Status", "Loading map...", Vector2.zero, new Vector2(700f, 60f), 24, LegacyUiKit.MutedColor);
            NextMapOverlay.Legend(card, new Vector2(Left, -280f));
            LabeledButton(card, "Back", "BACK", new Vector2(247f, -280f), new Vector2(130f, 52f), LegacyUiKit.PanelLineColor, Color.white, 20, true)
                .onClick.AddListener(Close);
            startButton = LabeledButton(card, "Start Wave", "Start Wave", new Vector2(431f, -280f), new Vector2(210f, 52f), Color.white, StartInk, 26, false);
            startButton.onClick.AddListener(StartWave);
        }

        private void BuildHeader(RectTransform card)
        {
            var name = BattleSceneFlow.IsTestRun ? "TEST LEVEL" : "LEVEL " + Mathf.Max(1, BattleSceneFlow.NextBattleLevelNumber);
            var title = LeftText(card, "Title", name, Left, 282f, 32, Color.white, FontStyles.Bold);
            var x = Left + title.GetPreferredValues(name).x + 26f;
            LegacyUiKit.CreateHeartIcon(card, Vector2.zero, 0.8f);
            var heart = (RectTransform)card.Find("Heart Icon");
            heart.anchorMin = heart.anchorMax = heart.pivot = new Vector2(0.5f, 0.5f);
            heart.anchoredPosition = new Vector2(x, 282f);
            var lives = BattleSceneFlow.CurrentLevelLivesRemaining + "/" + BattleSession.DefaultLivesPerLevel;
            var livesLabel = LeftText(card, "Lives", lives, x + 14f, 281f, 20, LegacyUiKit.TextColor, FontStyles.Bold);
            facts = LeftText(card, "Facts", string.Empty, x + 14f + livesLabel.GetPreferredValues(lives).x + 22f, 281f, 18, LegacyUiKit.MutedColor, FontStyles.Normal);
        }

        private static TMP_Text LeftText(Transform parent, string name, string text, float left, float y, float size, Color color, FontStyles style)
        {
            const float width = 600f;
            var label = LegacyUiKit.CreateText(parent, name, text, new Vector2(left + width / 2f, y), new Vector2(width, 44f), size, color, style);
            label.alignment = TextAlignmentOptions.Left;
            return label;
        }

        private static Button LabeledButton(Transform parent, string name, string text, Vector2 position, Vector2 size, Color fill, Color ink, float fontSize, bool bold)
        {
            var button = LegacyUiKit.CreateButton(parent, name, position, size, fill, 8);
            LegacyUiKit.CreateText(button.transform, "Label", text, Vector2.zero, size, fontSize, ink, bold ? FontStyles.Bold : FontStyles.Normal);
            return button;
        }

        private void StartWave()
        {
            if (owner != null) owner.StartWave();
        }

        private void OnPreviewSceneLoaded(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.path != path) return;
            SceneManager.sceneLoaded -= OnPreviewSceneLoaded;
            scene = loaded;
            Neutralize();
        }

        private void Neutralize()
        {
            if (neutralized || !scene.IsValid() || !scene.isLoaded) return;
            neutralized = true;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            }
        }

        private IEnumerator LoadMap()
        {
            try { load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive); }
            catch (System.Exception error) { status.text = "Unable to load map."; Debug.LogException(error, this); }
            if (load == null) { loadRunning = false; if (closed) yield return Finish(); yield break; }
            yield return load;
            if (!scene.IsValid()) scene = SceneManager.GetSceneByPath(path);
            Neutralize();
            if (closed) { loadRunning = false; yield return Finish(); yield break; }
            var prepared = false;
            try { prepared = PrepareMap(); }
            catch (System.Exception error) { Debug.LogException(error, this); }
            if (!prepared) { loadRunning = false; status.text = "Map preview unavailable"; yield break; }
            yield return null; // Let the camera render once before displaying its texture.
            loadRunning = false;
            if (closed) { yield return Finish(); yield break; }
            image.texture = texture;
            image.enabled = true;
            status.gameObject.SetActive(false);
            overlayRoot.gameObject.SetActive(true);
            IsReady = true;
        }

        private bool PrepareMap()
        {
            data = NextMapData.Capture(scene);
            if (!data.HasMap) return false;
            for (var i = 0; i < data.Towers.Count; i++)
            {
                var rangeObject = new GameObject("Preview Tower Range " + (i + 1));
                SceneManager.MoveGameObjectToScene(rangeObject, scene);
                var range = rangeObject.AddComponent<TowerRangeView>();
                range.BindPreview(data.Towers[i].Definition);
                foreach (var child in rangeObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
                ranges.Add(range);
            }
            var projection = NextMapProjection.Fit(BoardRotation, FitPoints(), MapSize, 8f, 30f, 8f);
            var height = Mathf.Clamp(Mathf.RoundToInt(MapSize.y * Screen.width / 1280f), 270, 1080);
            var width = Mathf.RoundToInt(height * MapSize.x / MapSize.y);
            texture = new RenderTexture(width, height, 24) { name = "Next Map", antiAliasing = 4 };
            texture.Create();
            var cameraObject = new GameObject("Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.targetTexture = texture;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = LegacyUiKit.CellColor;
            projection.Apply(previewCamera);
            var lightObject = new GameObject("Preview Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var lightSource = lightObject.AddComponent<Light>();
            lightSource.type = LightType.Directional;
            lightSource.intensity = 1.2f;
            lightSource.cullingMask = 1 << PreviewLayer;
            lightSource.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var count = data.Towers.Count;
            facts.text = count + (count == 1 ? " tower" : " towers") + "  |  road " + data.Route.Count + " tiles";
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.gameObject.AddComponent<NextMapOverlay>().Build(data, projection, ranges);
            overlayRoot.gameObject.SetActive(false);
            return true;
        }

        private IEnumerable<Vector3> FitPoints()
        {
            foreach (var corner in Corners(data.Cells)) yield return corner;
            foreach (var tower in data.Towers)
            {
                if (tower.HasModel)
                    foreach (var corner in Corners(tower.Model)) yield return corner;
                var lane = tower.Lane;
                foreach (var corner in Corners(new Bounds(lane.center + Vector3.up * 0.035f, lane.size))) yield return corner;
            }
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                        yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }

        private void StripPreviewLayer()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if ((camera.cullingMask & (1 << PreviewLayer)) == 0) continue;
                camera.cullingMask &= ~(1 << PreviewLayer);
                strippedCameras.Add(camera);
            }
        }

        private void RestoreLayer()
        {
            foreach (var camera in strippedCameras)
                if (camera != null) camera.cullingMask |= 1 << PreviewLayer;
            strippedCameras.Clear();
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            IsReady = false;
            if (!loadRunning) StartCoroutine(Finish());
        }

        private IEnumerator Finish()
        {
            if (finishing) yield break;
            finishing = true;
            if (previewCamera != null) previewCamera.enabled = false;
            image.enabled = false;
            overlayRoot.gameObject.SetActive(false);
            status.gameObject.SetActive(true);
            status.text = "Closing preview...";
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            BattleSceneFlow.EndLevelPreview(path);
            ended = true;
            RestoreLayer();
            if (owner != null) owner.OnNextLevelPreviewClosed();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnPreviewSceneLoaded;
            RestoreLayer();
            if (previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = null; }
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (ended) return;
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            if (load != null && !load.isDone)
            {
                var pendingPath = path;
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> cleanup = null;
                cleanup = (loaded, mode) =>
                {
                    if (loaded.path != pendingPath) return;
                    SceneManager.sceneLoaded -= cleanup;
                    foreach (var root in loaded.GetRootGameObjects()) root.SetActive(false);
                    SceneManager.UnloadSceneAsync(loaded);
                    BattleSceneFlow.EndLevelPreview(pendingPath);
                };
                SceneManager.sceneLoaded += cleanup;
            }
            else BattleSceneFlow.EndLevelPreview(path);
        }
    }
}
