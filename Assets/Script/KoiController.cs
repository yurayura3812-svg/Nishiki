using UnityEngine;

public class KoiSimpleRunner : MonoBehaviour
{
    void Start()
    {
        // Animatorを取得して再生状態にする（基本は自動再生されますが念のため）
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play("Scene");
        }
    }
}