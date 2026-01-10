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
        private bool showNameInputBox;

        public static SimpleConfirmationGUI CreateConfirmationGUI(string popup_text, bool show_name_input_box)
        {
            if (Instance != null)
            {
                Instance.popupText = popup_text;
                Instance.showPopup = true;
                Instance.showNameInputBox = show_name_input_box;
                return Instance;
            }
            GameObject obj = new GameObject("WendigosConfirmationGUI");
            var component = obj.AddComponent<SimpleConfirmationGUI>();
            component.popupText = popup_text;
            component.showNameInputBox = show_name_input_box;

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
            if (!showPopup) return;

            float scale = 1.25f;

            // Dimensions
            float sButtonWidth = buttonWidth * scale;
            float sButtonHeight = buttonHeight * scale;
            float sCloseSize = closeButtonSize * scale;

            // Window Dimensions
            float marginLeft = Screen.width * 0.1f;
            float marginTop = Screen.height * 0.1f;
            float windowWidth = Screen.width * 0.8f;
            float windowHeight = Screen.height * 0.8f;

            Rect backgroundRect = new Rect(marginLeft, marginTop, windowWidth, windowHeight);
            GUI.Box(backgroundRect, "Confirm Wendigos config sync");

            // ==========================================
            // 1. INPUT SECTION (Anchored to Top)
            // ==========================================
            if (showNameInputBox)
            {
                float fieldWidth = 200f * scale;
                float fieldHeight = 30f * scale;
                float labelHeight = 30f * scale;

                // Start position: just below the window title bar
                float startY = marginTop + (40f * scale);

                // 1a. Input Label
                GUIStyle centeredLabel = new GUIStyle(GUI.skin.label);
                centeredLabel.alignment = TextAnchor.MiddleCenter;
                centeredLabel.fontSize = (int)(16 * scale);

                GUI.Label(
                    new Rect(marginLeft, startY, windowWidth, labelHeight),
                    "Input your name so the mod knows who you are:",
                    centeredLabel
                );

                // 1b. Text Field (Placed immediately below label)
                float fieldY = startY + labelHeight + (5f * scale); // 5px gap
                float fieldX = marginLeft + (windowWidth - fieldWidth) / 2;

                GUIStyle inputFieldStyle = new GUIStyle(GUI.skin.textField);
                inputFieldStyle.fontSize = (int)(16 * scale);
                inputFieldStyle.alignment = TextAnchor.MiddleLeft;

                string newName = GUI.TextField(
                    new Rect(fieldX, fieldY, fieldWidth, fieldHeight),
                    Plugin.player_name.Value,
                    inputFieldStyle
                );

                if (newName != Plugin.player_name.Value)
                    Plugin.player_name.Value = newName;
            }

            // ==========================================
            // 2. CONFIRM SECTION (Anchored to Center)
            // ==========================================

            // Find the exact vertical center of the window
            float windowCenterY = marginTop + (windowHeight / 2);

            // Position the Button slightly below center
            float confirmY = windowCenterY + (10f * scale);
            float confirmX = marginLeft + (windowWidth - sButtonWidth) / 2;

            // Position the Text Area ABOVE the button
            if (wrappedMessageStyle == null)
            {
                wrappedMessageStyle = new GUIStyle(GUI.skin.label);
                wrappedMessageStyle.wordWrap = true;
                wrappedMessageStyle.alignment = TextAnchor.LowerCenter; // Text stacks from bottom up
                wrappedMessageStyle.fontSize = (int)(16 * scale);
                wrappedMessageStyle.normal.textColor = Color.white;
            }

            // Increased height to 160 to comfortably fit 3 lines
            float textHeight = 160f * scale;
            float textGap = 15f * scale; // Increased padding between text and button

            float textBottomY = confirmY - textGap;
            float textTopY = textBottomY - textHeight;

            // Added extra side padding (40f) so text doesn't touch edges
            Rect textRect = new Rect(
                marginLeft + (40f * scale),
                textTopY,
                windowWidth - (80f * scale), // Subtracting double the padding (left+right)
                textHeight
            );

            GUI.Label(textRect, popupText, wrappedMessageStyle);

            if (GUI.Button(new Rect(confirmX, confirmY, sButtonWidth, sButtonHeight), "CONFIRM"))
            {
                ConfirmAction();
            }

            // ==========================================
            // 3. CLOSE BUTTON (Top Right)
            // ==========================================
            float closeX = (marginLeft + windowWidth) - sCloseSize - (10f * scale);
            float closeY = marginTop + (10f * scale);

            if (GUI.Button(new Rect(closeX, closeY, sCloseSize, sCloseSize), "X"))
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