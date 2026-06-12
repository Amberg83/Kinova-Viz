using UnityEngine;
using UnityEngine.InputSystem;

public class DualCameraController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Camera freeMovingCamera;

    [Header("Top-Down Settings")]
    public float topDownPanSpeed = 15f;
    public float topDownZoomSpeed = 25f;
    public float minHeightOrZoom = 0f;
    public float maxHeightOrZoom = 40f;

    [Header("Free Move Settings")]
    public float freeMoveSpeed = 12f;
    public float freeMoveVerticalSpeed = 8f; // Speed for moving up/down
    public float lookSensitivity = 0.1f;

    private bool isTopDownActive = false;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        if (topDownCamera == null || freeMovingCamera == null)
        {
            Debug.LogError("[DualCameraController] Missing Camera references on " + gameObject.name + ". Please assign them in the inspector.", this);
            enabled = false;
            return;
        }

        SetCameraState(false);

        Vector3 currentRot = freeMovingCamera.transform.localEulerAngles;
        rotationX = currentRot.y;
        rotationY = -currentRot.x;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // Smart Swap Guard: Only switch cameras if we are NOT actively flying/looking around
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !Mouse.current.rightButton.isPressed)
        {
            isTopDownActive = !isTopDownActive;
            SetCameraState(isTopDownActive);
        }

        if (isTopDownActive)
        {
            HandleTopDownControls();
        }
        else
        {
            HandleFreeMoveControls();
        }
    }

    private void SetCameraState(bool topDownActive)
    {
        topDownCamera.gameObject.SetActive(topDownActive);
        freeMovingCamera.gameObject.SetActive(!topDownActive);

        if (topDownActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleTopDownControls()
    {
        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y = 1;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y = -1;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x = -1;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x = 1;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y) * topDownPanSpeed * Time.deltaTime;
        topDownCamera.transform.Translate(move, Space.World);

        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        float safeMinZoom = Mathf.Max(0.001f, minHeightOrZoom);

        if (topDownCamera.orthographic)
        {
            topDownCamera.orthographicSize = Mathf.Clamp(
                topDownCamera.orthographicSize - (scroll * topDownZoomSpeed * Time.deltaTime * 10f),
                safeMinZoom,
                maxHeightOrZoom
            );
        }
        else
        {
            Vector3 pos = topDownCamera.transform.position;
            pos.y = Mathf.Clamp(pos.y - (scroll * topDownZoomSpeed * Time.deltaTime * 50f), safeMinZoom, maxHeightOrZoom);
            topDownCamera.transform.position = pos;
        }
    }

    private void HandleFreeMoveControls()
    {
        bool isLooking = Mouse.current.rightButton.isPressed;

        if (isLooking)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            rotationX += mouseDelta.x * lookSensitivity;
            rotationY += mouseDelta.y * lookSensitivity;
            rotationY = Mathf.Clamp(rotationY, -85f, 85f);

            freeMovingCamera.transform.localRotation = Quaternion.Euler(-rotationY, rotationX, 0);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Horizontal input tracking (WASD)
        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) moveInput.y = 1;
        if (Keyboard.current.sKey.isPressed) moveInput.y = -1;
        if (Keyboard.current.aKey.isPressed) moveInput.x = -1;
        if (Keyboard.current.dKey.isPressed) moveInput.x = 1;

        // Vertical input tracking (Space to go Up, Left Shift to go Down)
        float verticalInput = 0f;
        if (Keyboard.current.spaceKey.isPressed) verticalInput = 1f;
        if (Keyboard.current.leftShiftKey.isPressed) verticalInput = -1f;

        // Apply Horizontal Movement
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = (freeMovingCamera.transform.forward * moveInput.y) + (freeMovingCamera.transform.right * moveInput.x);
            moveDirection.y = 0; // Keep WASD locked to a flat plane

            freeMovingCamera.transform.position += moveDirection.normalized * freeMoveSpeed * Time.deltaTime;
        }

        // Apply Vertical Movement separately
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            Vector3 upDirection = Vector3.up * verticalInput * freeMoveVerticalSpeed * Time.deltaTime;
            freeMovingCamera.transform.position += upDirection;
        }
    }
}