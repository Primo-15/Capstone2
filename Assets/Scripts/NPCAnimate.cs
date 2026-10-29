using System.Collections;
using UnityEngine;

public class NPCAnimate : MonoBehaviour
{
    private Animator animator;
    [SerializeField] private float animateTiggerRate = 1f;
    void Start()
    {
        animator = GetComponent<Animator>();
        StartCoroutine(AnimateNPC());
    }

    IEnumerator AnimateNPC()
    {
        while (true)
        {
            yield return new WaitForSeconds(animateTiggerRate);
            if (animator != null)
            {
                animator.SetTrigger("Talk");
            }
        }
    }


}
