// using UnityEngine;

// // 掛在貼紙面板的 Content 物件上（跟該物件上的 ObjectManipulator + ConstraintManager +
// // MoveAxisConstraint 搭配使用）：MoveAxisConstraint 負責鎖死 Y/Z，讓玩家滑動的手勢只能沿
// // 面板本地 X 軸移動；這支腳本補上 MoveAxisConstraint 沒做的部分——把可滑動範圍夾在
// // [minLocalX, maxLocalX] 之間，避免整排貼紙圖樣被滑到面板外面去。
// public class StickerPanelScrollClamp : MonoBehaviour
// {
//     [SerializeField] private float minLocalX = -0.3f;
//     [SerializeField] private float maxLocalX = 0f;

//     private void LateUpdate()
//     {
//         Vector3 position = transform.localPosition;
//         float clampedX = Mathf.Clamp(position.x, minLocalX, maxLocalX);
//         if (!Mathf.Approximately(clampedX, position.x))
//         {
//             transform.localPosition = new Vector3(clampedX, position.y, position.z);
//         }
//     }
// }
