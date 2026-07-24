using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

// 只在 Editor Play Mode 生效：用 ParrelSync 開兩份 Editor 測試雙人燒製時，
// 用來分辨「這個視窗現在扮演的是誰」，並讓兩邊在 Photon 房間裡有不同的暱稱方便肉眼確認。
// 打包成 HoloLens 版本後，UNITY_EDITOR 區塊會被整個排除，這支腳本在正式機上不會做任何事，可以留著不用移除。
//
// 用法：隨便掛在場景裡一個一開場就會執行的物件上即可（例如放 RoomLinker 的那個物件），
// 不需要在 Inspector 接任何欄位。
public class ParrelSyncTestLabel : MonoBehaviour
{
    private string _label = "P1 (主專案)";

    private void Awake()
    {
#if UNITY_EDITOR
        // 在 RoomLinker.Start() 呼叫 PhotonNetwork.ConnectUsingSettings() 之前，
        // 所有物件的 Awake() 一定會先跑完，所以這裡設定 NickName 一定來得及生效。
        if (ClonesManager.IsClone())
        {
            _label = "P2 (Clone)";
            PhotonNetwork.NickName = "P2_Clone";
        }
        else
        {
            PhotonNetwork.NickName = "P1_Main";
        }
        Debug.Log($"[ParrelSyncTestLabel] 這個 Editor 視窗代表 {_label}");
#endif
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 260, 26), $"測試身分：{_label}");
    }
#endif
}
