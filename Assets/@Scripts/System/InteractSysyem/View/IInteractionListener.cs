using UnityEngine;

/// <summary>
/// 상호작용 시스템의 View(UI)가 구현해야 하는 인터페이스입니다.
/// Presenter는 이 인터페이스를 통해 View와 통신하며, 상호작용 상태에 따른 UI 변경을 요청합니다.
/// </summary>
public interface IInteractionListener
{
    /// <summary>
    /// 상호작용 가능한 객체가 감지되었을 때 호출됩니다.
    /// </summary>
    /// <param name="tag">감지된 객체의 상호작용 종류를 나타내는 태그입니다.</param>
    void OnInteractDetected(string tag, string interactionText);

    /// <summary>
    /// 감지되었던 상호작용 가능한 객체가 범위를 벗어났을 때 호출됩니다.
    /// </summary>
    void OnInteractUnDetected();

    /// <summary>
    /// 플레이어가 상호작용을 수행했을 때 호출됩니다.
    /// </summary>
    /// <param name="tag">상호작용한 객체의 태그입니다.</param>
    void OnInteracted(string tag);

}