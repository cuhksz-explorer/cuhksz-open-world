#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MiddlegardenMapSystemConfigurator
{
    const string ScenePath = "Assets/scene/middlegarden.unity";
    const string MapSpritePath = "Assets/scene/textures/music_school.png";
    const string LocationIconPath = "Assets/pictures/downloaded-image (1).png";

    [MenuItem("Tools/Scene Config/Configure Middlegarden MapSystem")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var mapSystem = Object.FindObjectOfType<MapSystem>(true);
        if (mapSystem == null)
            throw new System.InvalidOperationException("MapSystem not found in middlegarden scene.");

        var mapWindow = FindRootGameObject(scene, "map") ?? CreateMapWindow();
        var mapImage = EnsureMapImage(mapWindow);
        var locationButtonTemplate = EnsureLocationButtonTemplate(mapImage.transform);

        var mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapSpritePath);
        if (mapSprite == null)
            throw new System.InvalidOperationException("Map sprite not found: " + MapSpritePath);

        var locationIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LocationIconPath);

        var image = mapImage.GetComponent<Image>();
        image.sprite = mapSprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        mapSystem.mapWindow = mapWindow;
        mapSystem.mapContainer = mapImage.GetComponent<RectTransform>();
        mapSystem.locationButtonPrefab = locationButtonTemplate;
        mapSystem.defaultLocationIcon = locationIcon;
        mapSystem.showPlayersOnBigMap = true;
        mapSystem.worldToMapUseAffineCalibration = true;
        mapSystem.worldToMapAffineAutoFitFromLocations = true;
        mapSystem.worldMinXZ = new Vector2(250f, -650f);
        mapSystem.worldMaxXZ = new Vector2(1300f, 220f);
        mapSystem.autoEstimateWorldBoundsFromLocations = false;
        mapSystem.mapLocations = new List<MapLocation>
        {
            new MapLocation
            {
                locationName = "librarymusic",
                worldPosition = new Vector3(1183.19995f, -6.65999985f, 148.910004f),
                uiPosition = new Vector2(230f, 158f),
                locationIcon = locationIcon
            },
            new MapLocation
            {
                locationName = "dorm",
                worldPosition = new Vector3(1107.76001f, -6.65999985f, -167.699997f),
                uiPosition = new Vector2(185f, 20f),
                locationIcon = locationIcon
            },
            new MapLocation
            {
                locationName = "runningway",
                worldPosition = new Vector3(1130.09998f, 6.86000013f, -116.449997f),
                uiPosition = new Vector2(198f, 43f),
                locationIcon = locationIcon
            }
        };

        EditorUtility.SetDirty(mapSystem);
        EditorUtility.SetDirty(mapWindow);
        EditorUtility.SetDirty(mapImage);
        EditorUtility.SetDirty(locationButtonTemplate);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MiddlegardenMapSystemConfigurator] Configured middlegarden MapSystem with music_school map and 3 teleport points.");
    }

    static GameObject FindRootGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    static GameObject CreateMapWindow()
    {
        var go = new GameObject("map", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);

        return go;
    }

    static GameObject EnsureMapImage(GameObject mapWindow)
    {
        var existing = mapWindow.transform.Find("Image");
        var go = existing != null
            ? existing.gameObject
            : new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        go.transform.SetParent(mapWindow.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(600f, 378f);
        rt.localScale = Vector3.one;
        return go;
    }

    static GameObject EnsureLocationButtonTemplate(Transform parent)
    {
        var existing = parent.Find("MapLocationButtonTemplate");
        var go = existing != null
            ? existing.gameObject
            : new GameObject("MapLocationButtonTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(24f, 24f);
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        go.SetActive(false);
        return go;
    }
}
#endif
