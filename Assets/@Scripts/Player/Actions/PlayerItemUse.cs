using System;
using UnityEngine;

/// <summary>
/// 플레이어가 보유하거나 퀵슬롯에 등록한 아이템의 사용 요청을 담당합니다.
/// </summary>
public sealed class PlayerItemUse : MonoBehaviour
{
    /// <summary>
    /// 포션 사용 요청이 아이템 사용 기능에 전달되면 발생합니다.
    /// </summary>
    public event Action PotionUseRequested;

    /// <summary>
    /// 아직 아이템 효과 적용이나 수량 차감 없이 포션 사용을 요청합니다.
    /// </summary>
    public void RequestPotionUse()
    {
        // TODO: 퀵슬롯 포션 확인, 효과 적용, 수량 차감, 서버 RPC를 구현합니다.
        Debug.Log("[TODO][PlayerItemUse] 포션 사용 요청.", this);
        PotionUseRequested?.Invoke();
    }
}
