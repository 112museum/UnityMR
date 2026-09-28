using UnityEngine;

// 掛在每一罐可以丟進 PigmentContainer 的顏料物件上（3 種顏色共用同一份腳本，
// 差別只在 Inspector 填的 pigmentId 不同）。單純標示「我是哪一種顏料」，
// 讓 PigmentContainer 的 OnTriggerEnter 判斷丟進來的是不是它要的那一種。
public class PigmentItem : MonoBehaviour
{
    [Header("這罐顏料的代號，要跟 PigmentContainer 設定的 pigmentId 完全一致（例如 red / yellow / blue）")]
    public string pigmentId;

    // 是不是已經被某個 PigmentContainer 算過一次了。VR 手抓丟擲的物件用的是
    // VelocityTracking + Rigidbody 物理，丟進容器的 trigger 範圍後常常還會因為
    // 碰撞到容器內壁而彈一下，於是同一次丟擲會讓 OnTriggerEnter 觸發不只一次，
    // 造成需要放兩個才能過關的顏色，丟一個就被扣了兩次直接完成。這個 flag 讓
    // PigmentContainer 保證同一罐顏料只會被計入一次。
    [System.NonSerialized] public bool hasBeenCounted;
}
