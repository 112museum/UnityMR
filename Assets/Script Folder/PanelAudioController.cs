// using UnityEngine;
// using UnityEngine.UI;
// using System.Collections;

// public class PanelAudioController : MonoBehaviour
// {
//     public GameObject panel;      // 你的 Panel
//     public PressableButton aButton;        // 按鈕，AudioSource 在這裡

//     private AudioSource buttonAudio; // 按鈕上的 AudioSource

//     void Start()
//     {
//         panel.SetActive(false); // 初始隱藏 Panel

//         // 取得按鈕上的 AudioSource
//         buttonAudio = aButton.GetComponent<AudioSource>();
//         if(buttonAudio == null)
//         {
//             Debug.LogWarning("按鈕上沒有 AudioSource！");
//         }

//         // 綁定按鈕事件
//         aButton.onClick.AddListener(OnAButtonPressed);
//     }

//     void OnAButtonPressed()
//     {
//         // 顯示 Panel
//         panel.SetActive(true);

//         if(buttonAudio != null)
//         {
//             buttonAudio.Play(); // 播放按鈕上的語音
//             StartCoroutine(HidePanelWhenAudioEnds());
//         }
//     }

//     IEnumerator HidePanelWhenAudioEnds()
//     {
//         // 等 AudioSource 播完
//         yield return new WaitWhile(() => buttonAudio.isPlaying);

//         // 隱藏 Panel
//         panel.SetActive(false);
//     }
// }