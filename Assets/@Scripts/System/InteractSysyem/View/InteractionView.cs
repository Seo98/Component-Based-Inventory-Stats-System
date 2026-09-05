using UnityEngine;

public class InteractionView : MonoBehaviour, IInteractionListener
{
    public void OnInteractDetected(string tag, string interactionText)
    {
        Debug.Log($"[View] 상호작용 감지: {tag}, {interactionText}");
    }

    public void OnInteracted(string tag)
    {
        Debug.Log($"[View] 상호작용 수행: {tag}");
    }

    public void OnInteractUnDetected()
    {
        Debug.Log("[View] 상호작용 감지 해제");
    }
}
