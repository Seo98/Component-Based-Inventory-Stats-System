/// <summary>
/// 캐릭터가 장착할 수 있는 실제 장비 위치를 구분합니다.
/// ItemData의 EquipmentSlot은 아이템 종류이고, 이 값은 반지 1/2처럼 런타임 위치를 구분합니다.
/// </summary>
public enum EquipmentLoadoutSlot : byte
{
    None = 0,
    Weapon = 1,
    Head = 2,
    Shoes = 3,
    Ring1 = 4,
    Ring2 = 5,
    Amulet = 6
}

/// <summary>
/// EquipmentLoadoutSlot을 네트워크 배열 인덱스로 안전하게 변환합니다.
/// </summary>
public static class EquipmentLoadoutSlotUtility
{
    public const int SlotCount = 6;

    /// <summary>실제로 장비를 저장할 수 있는 슬롯인지 확인합니다.</summary>
    public static bool IsValid(EquipmentLoadoutSlot slot)
    {
        return slot >= EquipmentLoadoutSlot.Weapon &&
               slot <= EquipmentLoadoutSlot.Amulet;
    }

    /// <summary>유효한 장비 슬롯을 0부터 시작하는 배열 인덱스로 변환합니다.</summary>
    public static int ToIndex(EquipmentLoadoutSlot slot)
    {
        return (int)slot - 1;
    }
}
