using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class InventoryItemUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI countText;

    /// <summary>
    /// 아이템 UI를 초기화하고 화면에 표시합니다.
    /// </summary>
    public void Setup(Sprite sprite, int count, bool isStackable, float rotation)
    {
        // 1. 스프라이트 및 회전 적용
        iconImage.sprite = sprite;
        iconImage.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);

        // 2. 스택(중첩) 가능한 아이템이고 개수가 2개 이상일 때만 텍스트 표시
        if (isStackable && count > 1)
        {
            countText.text = count.ToString();
            countText.enabled = true;
        }
        else
        {
            countText.enabled = false;
        }
    }

    /// <summary>
    /// 아이템이 제거되거나 풀에 반납될 때 상태를 초기화합니다.
    /// </summary>
    public void Clear()
    {
        iconImage.sprite = null;
        iconImage.rectTransform.localRotation = Quaternion.identity;
        countText.enabled = false;
    }
}
