using MixedReality.Toolkit.SpatialManipulation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 一次性工具：用來補上「貼貼紙」功能缺少的 StickerPlaced.prefab 與測試場景，
// 全部用簡單的基本圖形當佔位視覺，透過 Unity API 建立元件（而不是手改場景 yaml），
// 確保 PhotonView / ObjectManipulator 之類元件的序列化資料是 Unity 自己產生、保證正確。
// 用法：Unity 選單 Tools > Sticker > Build Test Setup，或用 -executeMethod 在 batchmode 執行。
public static class StickerTestSetup
{
    private const string StickerPrefabPath = "Assets/Resources/Sticker/StickerPlaced.prefab";
    private const string TestScenePath = "Assets/Scenes/Sticker_Test.unity";
    private const string MrtkXrRigGuid = "acbf65a81ce2cf94f82a0809298acf70";

    [MenuItem("Tools/Sticker/Build Test Setup")]
    public static void Run()
    {
        BuildStickerPlacedPrefab();
        BuildTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StickerTestSetup] Done.");
    }

    private static void BuildStickerPlacedPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sticker"))
            AssetDatabase.CreateFolder("Assets/Resources", "Sticker");

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "StickerPlaced";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        go.AddComponent<PhotonView>();
        var networked = go.AddComponent<StickerPlacedNetworked>();

        var so = new SerializedObject(networked);
        so.FindProperty("stickerRenderer").objectReferenceValue = go.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();

        bool ok;
        PrefabUtility.SaveAsPrefabAsset(go, StickerPrefabPath, out ok);
        Object.DestroyImmediate(go);

        Debug.Log(ok ? $"[StickerTestSetup] Prefab saved: {StickerPrefabPath}" : "[StickerTestSetup] Prefab save FAILED");
    }

    private static void BuildTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 玩家出生點 + MRTK XR Rig，位置設定照抄 FireSeed_Test.unity 的做法（世界原點、
        // 眼睛高度靠 Rig 內建的 Camera Offset +1.6m），確保跟已經驗證過的火種測試場景一致。
        var player = new GameObject("Player");
        player.tag = "Player";
        string rigPath = AssetDatabase.GUIDToAssetPath(MrtkXrRigGuid);
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
        if (rigPrefab == null)
        {
            Debug.LogError($"[StickerTestSetup] MRTK XR Rig prefab not found for guid {MrtkXrRigGuid}, path resolved to '{rigPath}'.");
        }
        else
        {
            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rigInstance.transform.SetParent(player.transform, false);
            rigInstance.transform.localPosition = Vector3.zero;
            rigInstance.transform.localRotation = Quaternion.identity;
        }

        var roomLinkerGO = new GameObject("RoomLinker");
        var roomLinker = roomLinkerGO.AddComponent<RoomLinker>();
        roomLinker.fixedRoomName = "MRMuseum";
        var bootstrap = roomLinkerGO.AddComponent<StickerTestBootstrap>();
        var bootstrapSo = new SerializedObject(bootstrap);
        bootstrapSo.FindProperty("roomLinker").objectReferenceValue = roomLinker;
        bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

        // 碗：放在正前方，跟出生點眼睛高度(1.6m)差不多、距離 3.5m，站著不用轉頭就看得到。
        var bowl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bowl.name = "Bowl";
        bowl.transform.position = new Vector3(0f, 1.2f, 3.5f);
        bowl.transform.localScale = Vector3.one * 0.4f;

        // 托盤＋貼紙：放在碗的右前方，同樣在視野內，方便一次同時看到碗跟托盤。
        var tray = new GameObject("Tray");
        tray.transform.position = new Vector3(0.5f, 1.3f, 2.5f);

        var sticker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sticker.name = "Sticker";
        sticker.transform.SetParent(tray.transform, false);
        sticker.transform.localPosition = Vector3.zero;
        sticker.transform.localScale = new Vector3(0.08f, 0.08f, 0.02f);

        sticker.AddComponent<ObjectManipulator>();
        var draggable = sticker.AddComponent<StickerDraggable>();
        var draggableSo = new SerializedObject(draggable);
        draggableSo.FindProperty("bowlTarget").objectReferenceValue = bowl.transform;
        draggableSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, TestScenePath);
        Debug.Log($"[StickerTestSetup] Scene saved: {TestScenePath}");
    }
}
