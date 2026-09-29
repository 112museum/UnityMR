using Photon.Pun;
using UnityEngine;

// 掛在跟 FireSeedButton 同一個 GameObject 上，該物件需要有 PhotonView 元件
// （Ownership Transfer 請設為 Takeover）。
// 哪一位玩家先按下這顆火種的按鈕，就由該玩家的裝置取得 ownership，之後由該裝置
// 負責把「是否按住」同步給房間裡的另一台 HoloLens；沒人碰過的火種預設由 MasterClient 控制。
[RequireComponent(typeof(FireSeedButton))]
[RequireComponent(typeof(PhotonView))]
public class PhotonFireSeed : MonoBehaviourPun, IPunObservable
{
    private FireSeedButton _button;

    // GetComponent 不受物件啟用狀態影響，Awake()/OnEnable() 才會；如果這顆火種一開始是關閉的
    // （例如「kiln 出現後、特定觸發才出現」這種流程），Awake() 在它第一次被 SetActive(true)
    // 之前根本不會執行，_button 會是 null。PhotonKilnBurningManager 卻是掛在一直啟用的窯爐上，
    // 從場景一開始每一幀都會呼叫這個屬性，所以改成惰性取得，不依賴 Awake() 一定跑過。
    private FireSeedButton Button => _button ??= GetComponent<FireSeedButton>();

    // 給 PhotonKilnBurningManager 讀取：不管本機是不是 owner，這裡永遠是目前同步後的按住狀態
    public bool IsHeldNetworked => Button.IsHeld;

    private void Awake()
    {
        _button = GetComponent<FireSeedButton>();
    }

    private void OnEnable()
    {
        Button.onPressStart.AddListener(HandleLocalPressStart);
    }

    private void OnDisable()
    {
        Button.onPressStart.RemoveListener(HandleLocalPressStart);
    }

    private void HandleLocalPressStart()
    {
        // 這顆火種還不是自己的才需要搶 ownership；已經是自己的話不用重複要求
        if (!photonView.IsMine)
        {
            photonView.RequestOwnership();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 本機是這顆火種目前的 owner，負責把本地按壓狀態廣播出去
            stream.SendNext(_button.IsHeld);
        }
        else
        {
            // 不是自己按的火種，只更新視覺（發光），不要觸發 onPressStart/onPressEnd
            bool held = (bool)stream.ReceiveNext();
            _button.SetHeldExternally(held);
        }
    }
}
