using UnityEngine;

// 掛在每一罐可以丟進 PigmentContainer 的顏料物件上（3 種顏色共用同一份腳本，
// 差別只在 Inspector 填的 pigmentId 不同）。單純標示「我是哪一種顏料」，
// 讓 PigmentContainer 的 OnTriggerEnter 判斷丟進來的是不是它要的那一種。
public class PigmentItem : MonoBehaviour
{
    [Header("這罐顏料的代號，要跟 PigmentContainer 設定的 pigmentId 完全一致（例如 red / yellow / blue）")]
    public string pigmentId;
}
