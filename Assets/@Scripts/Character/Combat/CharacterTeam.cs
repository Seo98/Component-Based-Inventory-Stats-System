using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 서버의 아군 공격 검사용 프리팹 팀 설정입니다.
    /// 0번 팀은 중립이며 모든 팀에게 공격받을 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterTeam : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _teamId;

        public int TeamId => _teamId;
    }
}
