using System.Collections.Generic;
using UnityEngine;

// 개인적으로 abstract가 더 적합해보임.
public interface IInteractable
{
    void TransformInteract(Transform Transform); 
    string GetInteractionText();
    string GetTag();
}
