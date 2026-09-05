using System;
using UnityEngine;

/// <summary>
/// 플레이어의 구르기 요청을 담당합니다.
/// 이동 거리, 무적 시간, 스태미나 등의 규칙은 추후 구현합니다.
/// </summary>
[RequireComponent(typeof(PlayerStamina))]
public sealed class PlayerRoll : MonoBehaviour
{
    /// <summary>
    /// 구르기 요청이 기능에 전달되면 발생합니다.
    /// </summary>
    public event Action RollRequested;

    /// <summary>
    /// 아직 캐릭터 이동이나 스태미나 소비 없이 구르기를 요청합니다.
    /// </summary>
    public void RequestRoll()
    {
        // TODO: 방향, 거리, 무적 시간, 스태미나, 애니메이션을 구현합니다.
        Debug.Log("[TODO][PlayerRoll] 구르기 요청.", this);

        RollRequested?.Invoke();
    }
}
