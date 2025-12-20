using System;
using UnityEngine;

namespace Wendigos
{
    public class SimpleConfirmationGUI : MonoBehaviour
    {
        private static SimpleConfirmationGUI Instance = null;
        // Toggle to show/hide the popup
        private bool showPopup = true;
        private string popupText;

        // Configuration for the UI appearance
        private float padding = 50f; // Padding from the edge of the screen
        private float buttonWidth = 120f;
        private float buttonHeight = 40f;
        private float closeButtonSize = 30f;

        private GUIStyle wrappedMessageStyle;

        public Action onButtonClicked;

        public static SimpleConfirmationGUI CreateConfirmationGUI(string popup_text)
        {
            if (Instance != null)
            {
                Instance.popupText = popup_text;
                Instance.showPopup = true;
                return Instance;
            }
            GameObject obj = new GameObject("WendigosConfirmationGUI");
            var component = obj.AddComponent<SimpleConfirmationGUI>();
            component.popupText = popup_text;

            return component;
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        void OnGUI()
        {
            // 1. If the popup is hidden, do not draw anything
            if (!showPopup) return;

            // 2. Define the background rectangle (Responsive to screen size)
            // We use 10% margins on all sides (0.1f) so it covers most of the screen
            float marginLeft = Screen.width * 0.1f;
            float marginTop = Screen.height * 0.1f;
            float windowWidth = Screen.width * 0.8f;
            float windowHeight = Screen.height * 0.8f;

            Rect backgroundRect = new Rect(marginLeft, marginTop, windowWidth, windowHeight);

            if (wrappedMessageStyle == null)
            {
                // Copy the default label style so we don't lose standard formatting
                wrappedMessageStyle = new GUIStyle(GUI.skin.label);
                wrappedMessageStyle.wordWrap = true; // <--- The Key Property
                wrappedMessageStyle.alignment = TextAnchor.MiddleCenter;
                wrappedMessageStyle.fontSize = 16;
                // distinct color to ensure readability
                wrappedMessageStyle.normal.textColor = Color.white;
            }

            // 3. Draw the background box
            GUI.Box(backgroundRect, "Confirm sync");

            // raw the Wrapping Text
            // We define a text area with some internal padding (e.g. 20px) inside the box
            float textPadding = 20f;
            float textWidth = windowWidth - (textPadding * 2);
            // We reserve space for the text between the top of box and the confirm button
            float availableTextHeight = windowHeight - buttonHeight - 60f;

            Rect textRect = new Rect(
                marginLeft + textPadding,
                marginTop + 1f, // Push down slightly to avoid the "Confirmation" title
                textWidth,
                availableTextHeight
            );

            GUI.Label(textRect, popupText, wrappedMessageStyle);

            // 4. Draw the "Confirm" Action Button (Centered in the box)
            float confirmX = marginLeft + (windowWidth - buttonWidth) / 2;
            float confirmY = marginTop + (windowHeight - buttonHeight) / 2;

            if (GUI.Button(new Rect(confirmX, confirmY, buttonWidth, buttonHeight), "CONFIRM"))
            {
                ConfirmAction();
            }

            // 5. Draw the "Cancel" Button (Top-Right of the box)
            // We align it to the top-right corner of the backgroundRect
            float closeX = (marginLeft + windowWidth) - closeButtonSize - 10f; // 10f is a small internal margin
            float closeY = marginTop + 10f;

            if (GUI.Button(new Rect(closeX, closeY, closeButtonSize, closeButtonSize), "X"))
            {
                ClosePopup();
            }
        }

        // Logic for the Confirm Button
        void ConfirmAction()
        {
            onButtonClicked?.Invoke();
            // Optional: Hide the GUI after confirming
            showPopup = false;
            Destroy(gameObject);
        }

        // Logic for the Cancel Button
        void ClosePopup()
        {
            showPopup = false;
            Destroy(gameObject);
        }
    }
}