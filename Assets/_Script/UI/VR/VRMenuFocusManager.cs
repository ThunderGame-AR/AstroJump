using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VRMenuFocusManager : MonoBehaviour
{
    public static VRMenuFocusManager Instance;

    private void Awake() => Instance = this;

    public void FocusButton(Button targetButton)
    {
        if (targetButton == null || !targetButton.gameObject.activeInHierarchy) return;

        EventSystem.current.SetSelectedGameObject(null);
        targetButton.Select();
        targetButton.OnSelect(new BaseEventData(EventSystem.current));
    }
}