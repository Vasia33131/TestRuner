using UnityEngine;

namespace ButchersGames.Player
{
    /// <summary>
    /// Reads sideways steering from a mouse/finger drag and from the keyboard (A/D, arrows).
    /// Knows nothing about the road: it only reports how far the player wants to move sideways this frame.
    /// </summary>
    public sealed class SteeringInput
    {
        private float lastPointerX;
        private bool isDragging;

        /// <summary>Drops the current drag, so a touch that started before a pause is not continued.</summary>
        public void Reset()
        {
            isDragging = false;
        }

        /// <summary>
        /// Lateral offset change requested this frame, in world units.
        /// swipeScale converts a drag over the whole screen width into world units,
        /// keyboardSpeed is in world units per second.
        /// </summary>
        public float ReadLateralDelta(float swipeScale, float keyboardSpeed, float deltaTime)
        {
            return ReadPointer(swipeScale) + ReadKeyboard(keyboardSpeed, deltaTime);
        }

        private float ReadPointer(float swipeScale)
        {
            float delta = 0f;
            float pointerX = Input.mousePosition.x;

            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastPointerX = pointerX;
            }

            if (Input.GetMouseButton(0) && isDragging && Screen.width > 0)
            {
                delta = (pointerX - lastPointerX) / Screen.width * swipeScale;
                lastPointerX = pointerX;
            }

            if (Input.GetMouseButtonUp(0))
                isDragging = false;

            return delta;
        }

        private static float ReadKeyboard(float speed, float deltaTime)
        {
            float axis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) axis -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) axis += 1f;
            return axis * speed * deltaTime;
        }
    }
}
