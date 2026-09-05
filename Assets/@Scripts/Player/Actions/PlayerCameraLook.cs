using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 1인칭 시점 입력에 따라 플레이어 몸통을 좌우로 회전하고 카메라 타깃을 상하로 회전합니다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerCameraLook : NetworkBehaviour
{
    [Header("시점 조작")]
    [SerializeField, Min(0f)] private float _sensitivity = 2.5f;
    [SerializeField, Range(-89f, 0f)] private float _minimumPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float _maximumPitch = 80f;

    private Transform _lookTarget;
    private Renderer[] _playerRenderers;
    private float _yaw;
    private float _pitch;
    [SerializeField] private bool _hideOwnerRenderers = true;

    private void Awake()
    {
        _lookTarget = transform.Find("FollowTarget");

        if (_lookTarget == null)
        {
            Debug.LogError("[PlayerCameraLook] FollowTarget 자식 오브젝트를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        _yaw = transform.eulerAngles.y;
        _pitch = NormalizeAngle(_lookTarget.localEulerAngles.x);
    }

    /// <summary>
    /// 소유 클라이언트에서 1인칭 시야를 가리는 자신의 캐릭터 렌더러를 숨깁니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsOwner && _hideOwnerRenderers)
            SetPlayerRenderersHidden(true);
    }

    /// <summary>
    /// 네트워크 디스폰 시 숨겼던 캐릭터 렌더러를 복구합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsOwner && _hideOwnerRenderers)
            SetPlayerRenderersHidden(false);
    }

    /// <summary>
    /// 좌우 입력은 몸통 회전에, 제한된 상하 입력은 카메라 타깃 회전에 적용합니다.
    /// </summary>
    public void ApplyLookInput(Vector2 input)
    {
        if (!CanLookLocally() || _lookTarget == null || input.sqrMagnitude <= 0f)
            return;

        _yaw = Mathf.Repeat(_yaw + input.x * _sensitivity, 360f);
        _pitch = Mathf.Clamp(
            _pitch - input.y * _sensitivity,
            _minimumPitch,
            _maximumPitch);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        _lookTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private bool CanLookLocally()
    {
        return !IsSpawned || IsOwner;
    }

    private void SetPlayerRenderersHidden(bool isHidden)
    {
        if (_playerRenderers == null)
            _playerRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer playerRenderer in _playerRenderers)
        {
            if (playerRenderer != null)
                playerRenderer.forceRenderingOff = isHidden;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
