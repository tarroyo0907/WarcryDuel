using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatMoveButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
{
    [SerializeField] private bool buttonPressed;
    [SerializeField] private float originalWidth;
    [SerializeField] private float originalHeight;

    // Button Components
    [SerializeField] private Button button;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image image;

    // Start is called before the first frame update
    void Start()
    {
        // Grabs a reference to its components
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        // Grabs its original width and height
        originalWidth = rectTransform.sizeDelta.x;
        originalHeight = rectTransform.sizeDelta.y;
    }

    // Update is called once per frame
    void Update()
    {
        // Checks if the button is being pressed
        GameObject buttonOverlay = transform.GetChild(0).gameObject;
        if (buttonPressed && !buttonOverlay.active)
        {
            // Shrinks the button's width and height
            rectTransform.sizeDelta = new Vector2(originalWidth * 0.8f, originalHeight * 0.8f);

            // Changes the buttons color to gray
            image.color = Color.gray;

        }
        // If button is not pressed
        else
        {
            // Returns the buttons size to its original width and height
            rectTransform.sizeDelta = new Vector2(originalWidth, originalHeight);

            // Returns the buttons color back to white
            image.color = Color.white;
        }
    }

    void IPointerDownHandler.OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        buttonPressed = true;
    }

    void IPointerUpHandler.OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        buttonPressed = false;
    }
}
