using UnityEngine;

/// <summary>
/// MVP(Model-View-Presenter) 패턴에 따라 플레이어 상호작용 로직(Model)과 UI(View)를 연결하는 Presenter입니다.
/// </summary>
/// <remarks>
/// `PlayerInteract`(Model)로부터 상호작용 감지/해제/완료 이벤트를 수신하여,
/// `InteractionUI`(View)에 해당 정보를 전달해 UI를 업데이트하는 역할을 합니다.
/// </remarks>
public class InteractionPresenter
{
    private readonly IInteractionListener _view;
    private readonly PlayerInteractController _controller;

    /// <summary>
    /// InteractionPresenter를 생성하고, Model과 View를 연결하여 이벤트 구독을 설정합니다.
    /// </summary>
    /// <param name="view">상호작용 UI(View)</param>
    /// <param name="model">상호작용 로직(Model)</param>
    public InteractionPresenter(IInteractionListener view, PlayerInteractController controller)
    {
        _view = view;
        _controller = controller;

        // --- 이벤트 구독 부분 추가 ---
        _controller.OnInteractDetected += HandleInteractDetected;
        _controller.OnInteractUndetected += HandleInteractUndetected;
        _controller.OnInteracted += HandleInteracted;
    }



    #region 이벤트 수신 핸들러
    /// <summary>
    /// 감지(Detect) 이벤트를 Presenter가 수신했을 때 호출되는 함수.
    /// 해당 오브젝트의 태그 정보를 전달받아 View에 전달하며,  
    /// View는 이를 통해 상호작용 가능한 오브젝트가 범위 내에 들어왔음을 표시한다.
    /// </summary>
    /// <param name="tag">감지된 오브젝트의 태그</param>
    private void HandleInteractDetected(string tag, string interactionText)
    {
        Debug.Log("[Presenter] 감지 이벤트 수신");
        _view.OnInteractDetected(tag, interactionText);
    }

    /// <summary>
    /// 감지 해제(Undetect) 이벤트를 Presenter가 수신했을 때 호출되는 함수.
    /// 상호작용 가능 오브젝트가 범위를 벗어났음을 View에 알린다.
    /// </summary>
    private void HandleInteractUndetected()
    {
        Debug.Log("[Presenter] 감지 해제 이벤트 수신");
        _view.OnInteractUnDetected();
    }

    /// <summary>
    /// 실제 상호작용(Interact) 입력이 발생했을 때 호출되는 함수.
    /// 감지된 대상 태그를 전달하여 View가 상호작용 연출(예: 애니메이션, UI 효과 등)을 수행하도록 한다.
    /// </summary>
    /// <param name="tag">상호작용한 오브젝트의 태그</param>
    private void HandleInteracted(string tag)
    {
        Debug.Log("[Presenter] 상호작용 이벤트 수신");
        _view.OnInteracted(tag);
    }
    #endregion

}