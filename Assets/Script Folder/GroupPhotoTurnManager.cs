using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// 大合照兩人輪流拍照的狀態機。GroupPhotoCapture 只負責「呼叫 HoloLens 相機拍一張」，
// GroupPhotoEmailPrompt 只負責「信箱輸入面板 + 寄送」，這支腳本負責中間那段：
// 誰先拍、誰等待、倒數、拍照瞬間要把 UI 清空避免擋到鏡頭、雙方都拍完才進信箱畫面。
//
// 角色判定沿用 StoryModeManager 既有慣例（PhotonNetwork.LocalPlayer.ActorNumber == 1
// 判斷「先進房間的人」）：ActorNumber 1 = 先幫忙拍照的人，ActorNumber 2 = 先被拍照的人。
// 「對方拍完了沒」用 Photon Player Custom Property（groupPhotoCaptured）同步，這樣不管
// 是誰先到，晚到的一方也能立刻查到已經存在的狀態，不會漏接一次性事件。
//
// 掛在跟 GroupPhotoCapture / GroupPhotoEmailPrompt 同一個 GameObject 上。
public class GroupPhotoTurnManager : MonoBehaviourPunCallbacks
{
    private const string CapturedPropKey = "groupPhotoCaptured";
    private const int CountdownSeconds = 3;

    private enum TurnState { WaitingForPartner, ReadyToShoot, CountingDown, Capturing, WaitingPartnerFinish, ReadyForEmail }

    [Header("依賴")]
    public GroupPhotoCapture photoCapture;
    public GroupPhotoEmailPrompt emailPrompt;

    [Header("UI（沿用 GroupPhotoTrigger 底下既有的提示文字／按鈕）")]
    public TMP_Text hintText;
    public Button photoButton;
    public GameObject triggerPanel;

    private TurnState state;
    private bool isFirstPhotographer;
    private bool _orderDecided;

    private void Start()
    {
        // Debug.Log($"[GroupPhotoTurnManager] Start() 執行, ActorNumber={PhotonNetwork.LocalPlayer.ActorNumber}, " +
        //           $"hintText={(hintText != null)}, photoButton={(photoButton != null)}, triggerPanel={(triggerPanel != null)}");

        if (photoCapture != null)
        {
            photoCapture.onPhotoSaved.AddListener(HandlePhotoSaved);
            photoCapture.onPhotoFailed.AddListener(HandlePhotoFailed);
        }

        TryDecidePhotographerOrder();
    }

    public override void OnJoinedRoom()
    {
        // Debug.Log("[GroupPhotoTurnManager] OnJoinedRoom()");
        TryDecidePhotographerOrder();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Debug.Log($"[GroupPhotoTurnManager] OnPlayerEnteredRoom({newPlayer.ActorNumber})");
        TryDecidePhotographerOrder();
    }

    private void TryDecidePhotographerOrder()
    {
        if (_orderDecided)
        {
            Debug.Log("[GroupPhotoTurnManager] TryDecidePhotographerOrder() 略過，已經決定過順序");
            return;
        }

        // 加入房間的按鈕跟這個場景的 Start() 是各自獨立觸發的，玩家不一定在這個
        // GameObject 的 Start() 執行時就已經按過按鈕，所以要先確認真的在房間裡，
        // 不能直接讀 PhotonNetwork.CurrentRoom(還沒加入房間時是 null，會噴 NRE)。
        if (!PhotonNetwork.InRoom)
        {
            Debug.Log("[GroupPhotoTurnManager] 尚未加入 Photon 房間，先等待");
            EnterWaitingForPartner("等待加入房間...");
            return;
        }

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        // Debug.Log($"[GroupPhotoTurnManager] TryDecidePhotographerOrder() PlayerCount={playerCount}");

        if (playerCount < 2)
        {
            EnterWaitingForPartner("等待夥伴加入...");
            return;
        }

        _orderDecided = true;
        isFirstPhotographer = PhotonNetwork.LocalPlayer.ActorNumber == GetMinActorNumberInRoom();
        // Debug.Log($"[GroupPhotoTurnManager] isFirstPhotographer={isFirstPhotographer}");

        if (isFirstPhotographer)
            EnterReadyToShoot("請先幫對方開始拍照！");
        else
            EnterWaitingForPartner("請先讓對方幫您拍照！");
    }

    // 掛在 Take Photo Button 的 OnClick（取代原本直接呼叫 GroupPhotoCapture.TakeGroupPhoto()）。
    public void OnPhotoButtonClicked()
    {
        if (state != TurnState.ReadyToShoot) return;
        StartCoroutine(CountdownThenCapture());
    }

    private IEnumerator CountdownThenCapture()
    {
        state = TurnState.CountingDown;
        SetButtonVisible(false);

        for (int remaining = CountdownSeconds; remaining > 0; remaining--)
        {
            SetHintText(remaining.ToString());
            yield return new WaitForSeconds(1f);
        }

        // 倒數結束，整個提示面板（文字＋按鈕）收起來，避免這些 UI 擋在鏡頭前面
        // 影響 HoloLens 光學透視拍到的畫面——這是這次要修的原始 bug。
        if (triggerPanel != null) triggerPanel.SetActive(false);

        state = TurnState.Capturing;
        if (photoCapture != null) photoCapture.TakeGroupPhoto();
    }

    private void HandlePhotoFailed(string errorMessage)
    {
        if (triggerPanel != null) triggerPanel.SetActive(true);
        EnterReadyToShoot($"拍照失敗：{errorMessage}，請再試一次");
    }

    private void HandlePhotoSaved(string photoPath)
    {
        if (string.IsNullOrEmpty(photoPath))
        {
            // Editor/非 UWP 平台沒有真的拍照，但輪流的狀態機邏輯本身還是可以往下走，
            // 方便不接 HoloLens、單純用兩個連進同一個 Photon 房間的 Editor/Build 測試輪流同步。
            Debug.Log("[GroupPhotoTurnManager] 沒有實際照片檔案（非 HoloLens 平台），仍照常推進輪流狀態。");
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { CapturedPropKey, true } });

        if (isFirstPhotographer)
        {
            if (IsPartnerCaptured())
            {
                EnterReadyForEmail();
            }
            else
            {
                EnterWaitingPartnerFinish("換對方幫您拍照了！請稍候...");
            }
        }
        else
        {
            EnterReadyForEmail();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer == PhotonNetwork.LocalPlayer) return;
        if (!changedProps.TryGetValue(CapturedPropKey, out var captured) || !(bool)captured) return;

        if (state == TurnState.WaitingForPartner)
        {
            EnterReadyToShoot("換你幫對方拍照了！");
        }
        else if (state == TurnState.WaitingPartnerFinish)
        {
            EnterReadyForEmail();
        }
    }

    // 「先幫忙拍照的人」＝目前房間裡還活著的玩家中 ActorNumber 最小的那個，而不是寫死
    // 比對 1 號——固定房間反覆測試很多次之後，ActorNumber 會一直往上累加，兩位目前真正
    // 在場的玩家不一定剛好是 1 號、2 號。
    private int GetMinActorNumberInRoom()
    {
        int min = PhotonNetwork.LocalPlayer.ActorNumber;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p.ActorNumber < min) min = p.ActorNumber;
        }
        return min;
    }

    private bool IsPartnerCaptured()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p == PhotonNetwork.LocalPlayer) continue;
            return p.CustomProperties.TryGetValue(CapturedPropKey, out var captured) && (bool)captured;
        }
        return false;
    }

    private void EnterWaitingForPartner(string hint)
    {
        state = TurnState.WaitingForPartner;
        SetButtonVisible(false);
        SetHintText(hint);
    }

    private void EnterReadyToShoot(string hint)
    {
        state = TurnState.ReadyToShoot;
        if (triggerPanel != null) triggerPanel.SetActive(true);
        SetButtonVisible(true);
        SetHintText(hint);
    }

    private void EnterWaitingPartnerFinish(string hint)
    {
        state = TurnState.WaitingPartnerFinish;
        if (triggerPanel != null) triggerPanel.SetActive(true);
        SetButtonVisible(false);
        SetHintText(hint);
    }

    private void EnterReadyForEmail()
    {
        state = TurnState.ReadyForEmail;
        if (triggerPanel != null) triggerPanel.SetActive(false);
        if (emailPrompt != null) emailPrompt.ShowPanel();
    }

    private void SetButtonVisible(bool visible)
    {
        if (photoButton != null)
            photoButton.gameObject.SetActive(visible);
        else
            Debug.LogWarning("[GroupPhotoTurnManager] SetButtonVisible() 被呼叫但 photoButton 是 null，UI 不會有變化");
    }

    private void SetHintText(string text)
    {
        if (hintText != null)
            hintText.text = text;
        else
            Debug.LogWarning($"[GroupPhotoTurnManager] SetHintText(\"{text}\") 被呼叫但 hintText 是 null，UI 不會有變化");
    }
}
