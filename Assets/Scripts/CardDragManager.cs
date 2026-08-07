
using UnityEngine;
using UnityEngine.InputSystem;

public class CardDragManager : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            Debug.Log($"클릭: {mousePos}");

            CardCanvas.StartCardDrag(mousePos);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Debug.Log("마우스 뗌");

            CardCanvas.EndCardDrag();
        }
    }
}