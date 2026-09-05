using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 아이템의 장착 슬롯을 정의합니다.
/// </summary>
public enum EquipmentSlot
{
    None,
    Weapon,
    Head,
    Shoes,
    Ring,
    Amulet,
    Quick
}

/// <summary>
/// 손에 들었을 때 사용할 비주얼/자세 타입을 정의합니다.
/// </summary>
public enum HandheldType
{
    None = 0,
    Dagger = 1,
    Mace = 2,
    Pickaxe = 3,
    Spear = 4,
    Slingshot = 5,
    ShortBow = 6,
    Shield = 7,
    Staff = 8,
    GreatSword = 9,
    Ballista = 10,
    Utility = 99
}

public enum ItemRank
{
    Common = 1,
    Magic = 2,
    Rare = 3,
    Epic = 4,
    Legendary = 5,
    God = 6
}


/// <summary>
/// 아이템 사용 시 처리 방식을 정의합니다.
/// </summary>
public enum ItemUseMechanic
{
    None,
    ConsumeStack,
    Durability,
    PlaceObject,
    EnchantWeapon,
    LoadToObject
}

/// <summary>
/// 아이템의 원본 속성(이름, 인벤토리 모양, 이미지, 손 비주얼, 사용 방식 등)을 정의하는 데이터 클래스입니다.
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Inventory/Item", order = 1)]
public class ItemData : ScriptableObject
{
    private static int _globalIdCounter = 1;

    [Header("Basic Settings")]
    [SerializeField] private int _itemID;
    [SerializeField] private string _itemName = "New Item";
    [SerializeField] private Sprite _sprite;
    [SerializeField] private Sprite _outlineSprite;
    [SerializeField] private ItemType _type = ItemType.Utility;
    [SerializeField] private bool _canDrop = true;
    [SerializeField, Min(0)] private int _buyGold;
    [SerializeField, Min(0)] private int _sellGold;

    [Header("Text Settings")]
    [SerializeField, TextArea(2, 4)] private string _description;

    [Header("Rank Settings")]
    [SerializeField] private ItemRank _rank = ItemRank.Common;

    [Header("Inventory Settings")]
    [SerializeField] private InventoryShape _shape;
    [SerializeField, Min(1)] private int _maxStackCount = 1;
    [SerializeField, Min(0f)] private float _weight = 0f;

    [Header("Equip / Use Settings")]
    [SerializeField] private EquipmentSlot _equipSlot = EquipmentSlot.None;
    [SerializeField] private ItemUseMechanic _useMechanic = ItemUseMechanic.None;
    [SerializeField] private bool _canHoldInHand;
    [SerializeField] private HandheldType _handheldType = HandheldType.None;
    [SerializeField, Min(0)] private int _maxDurability;

    [Header("Identification Settings")]
    [SerializeField] private bool _requiresIdentification;
    [SerializeField, Min(0)] private int _requiredIntelligence;
    [SerializeField] private string _unidentifiedName = "정체불명의 아이템";
    [SerializeField, TextArea(2, 4)] private string _unidentifiedDescription;

    [Header("Combat Settings")]
    [SerializeField] private float _correctionValue;

    [Header("Equipment Stat Modifiers")]
    [Tooltip("이 아이템을 장비 슬롯에 장착했을 때 적용되는 스탯 변경값입니다.")]
    [SerializeField] private ItemStatModifierData[] _statModifiers = Array.Empty<ItemStatModifierData>();

    [Header("Model Settings")]
    [Tooltip("메쉬/머티리얼 추출용 순수 비주얼 프리팹입니다. 콜리더/상호작용 로직은 넣지 않습니다.")]
    [SerializeField] private AssetReferenceGameObject _modelReference;

    [Tooltip("특수 드랍 프리팹이 필요한 아이템만 지정합니다. 비워두면 공용 드랍 프리팹을 사용합니다.")]
    [SerializeField] private AssetReferenceGameObject _worldPrefabOverride;

    [Tooltip("모델 프리팹에서 자동 추출된 메쉬입니다.")]
    [SerializeField] private Mesh _mesh;

    [Tooltip("모델 프리팹에서 자동 추출된 머티리얼입니다.")]
    [SerializeField] private Material[] _materials;


    [SerializeField] private int upgrade;

    public int Upgrade => upgrade;

    public float CorrectionValue => _correctionValue;

    /// <summary>
    /// 장착 시 적용할 스탯 변경값 목록입니다.
    /// </summary>
    public IReadOnlyList<ItemStatModifierData> StatModifiers =>
        _statModifiers ?? Array.Empty<ItemStatModifierData>();
    /// <summary>
    /// UI 툴팁에 표시할 기능 설명입니다.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// 아이템 1개당 무게입니다.
    /// </summary>
    public float Weight => _weight;

    public int ItemID => _itemID;

    /// <summary>아이템 표시 이름입니다.</summary>
    public string Name => _itemName;

    /// <summary>인벤토리에서 표시할 기본 아이콘입니다.</summary>
    public Sprite Sprite => _sprite;

    /// <summary>인벤토리 선택/외곽선 표시용 아이콘입니다.</summary>
    public Sprite OutlineSprite => _outlineSprite;

    /// <summary>아이템의 대분류입니다.</summary>
    public ItemType Type => _type;

    /// <summary>필드에 버릴 수 있는지 여부입니다.</summary>
    public bool CanDrop => _canDrop;

    /// <summary>상점 판매 가격입니다.</summary>
    public int SellGold => _sellGold;
    public int BuyGold => _buyGold;
    public ItemRank Rank => _rank;

    /// <summary>인벤토리에서 차지하는 원본 모양입니다.</summary>
    public InventoryShape OriginalShape => _shape;

    /// <summary>최대 스택 수입니다.</summary>
    public int MaxStackCount => _maxStackCount;

    /// <summary>스택 가능한 아이템인지 여부입니다.</summary>
    public bool IsStackable => _maxStackCount > 1;

    /// <summary>장착 가능한 슬롯입니다.</summary>
    public EquipmentSlot EquipSlot => _equipSlot;

    /// <summary>아이템 사용 처리 방식입니다.</summary>
    public ItemUseMechanic UseMechanic => _useMechanic;

    /// <summary>손에 들 수 있는지 여부입니다.</summary>
    public bool CanHoldInHand => _canHoldInHand;

    /// <summary>손에 들었을 때 사용할 비주얼/자세 타입입니다.</summary>
    public HandheldType HandheldType => _handheldType;

    /// <summary>내구도형 아이템의 최대 내구도입니다. 0이면 내구도 없음으로 취급합니다.</summary>
    public int MaxDurability => _maxDurability;

    /// <summary>손/필드 표시용 공용 모델 프리팹 참조입니다.</summary>
    public AssetReferenceGameObject ModelReference => _modelReference;

    /// <summary>특수 드롭 전용 프리팹 참조입니다. 비어 있으면 공용 드랍 프리팹을 사용합니다.</summary>
    public AssetReferenceGameObject WorldPrefabOverride => _worldPrefabOverride;

    /// <summary>비주얼 매니저에서 메쉬 스왑에 사용할 메쉬입니다.</summary>
    public Mesh Mesh => _mesh;

    /// <summary>비주얼 매니저에서 메쉬 스왑에 사용할 머티리얼 목록입니다.</summary>
    public Material[] Materials => _materials;

    public bool RequiresIdentification => _requiresIdentification;
    public int RequiredIntelligence => _requiredIntelligence;
    public string UnidentifiedName => _unidentifiedName;
    public string UnidentifiedDescription => _unidentifiedDescription;

    public bool CanIdentify(int intelligence)
    {
        return !_requiresIdentification || intelligence >= _requiredIntelligence;
    }

    /// <summary>
    /// 이 데이터를 기반으로 실제 인벤토리에 들어갈 아이템 인스턴스를 생성합니다.
    /// </summary>
    public ItemInstance CreateInstance()
    {
        return new ItemInstance(this, _globalIdCounter++);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_maxStackCount < 1)
            _maxStackCount = 1;

        if (!_canHoldInHand)
            _handheldType = HandheldType.None;

        if (_modelReference == null || _modelReference.editorAsset == null)
            return;

        if (_modelReference.editorAsset is not GameObject modelPrefab)
            return;

        MeshFilter meshFilter = modelPrefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer meshRenderer = modelPrefab.GetComponentInChildren<MeshRenderer>();

        if (meshFilter != null)
            _mesh = meshFilter.sharedMesh;

        if (meshRenderer != null)
            _materials = meshRenderer.sharedMaterials;
    }
#endif
}
