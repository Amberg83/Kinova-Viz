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
    // Set safe minimum bounds so the camera never goes below or exactly to 0
    public float minHeightOrZoom = 2f;
    public float maxHeightOrZoom = 40f;

    [Header("Free Move Settings")]
    public float freeMoveSpeed = 12f;
    public float lookSensitivity = 0.1f;

    private bool isTopDownActive = true;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Missing Reference Guard
        if (topDownCamera == null || freeMovingCamera == null)
        {
            Debug.LogError("[DualCameraController] Missing Camera references on " + gameObject.name + ". Please assign them in the inspector.", this);
            enabled = false;
            return;
        }

        SetCameraState(true);
        Vector3 currentRot = freeMovingCamera.transform.localEulerAngles;
        rotationX = currentRot.y;
        rotationY = -currentRot.x;
    }

    void Update()
    {
        // Device Check Guard
        if (Keyboard.current == null || Mouse.current == null) return;

        // Toggle camera mode with Spacebar
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
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

        // Zoom Management with Value Guard Rails
        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;

        // Ensure min Zoom is never 0 or negative to prevent viewport collapse errors
        float safeMinZoom = Mathf.Max(0.1f, minHeightOrZoom);

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
            // Using Time.deltaTime ensures scroll rate matches frame variance safely
            pos.y = Mathf.Clamp(pos.y - (scroll * topDownZoomSpeed * Time.deltaTime * 50f), safeMinZoom, maxHeightOrZoom);
            topDownCamera.transform.position = pos;
        }
    }

    private void HandleFreeMoveControls()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            rotationX += mouseDelta.x * lookSensitivity;
            rotationY += mouseDelta.y * lookSensitivity;

            // Hard limit look angles to prevent camera flipping bugs
            rotationY = Mathf.Clamp(rotationY, -85f, 85f);

            freeMovingCamera.transform.localRotation = Quaternion.Euler(-rotationY, rotationX, 0);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) moveInput.y = 1;
        if (Keyboard.current.sKey.isPressed) moveInput.y = -1;
        if (Keyboard.current.aKey.isPressed) moveInput.x = -1;
        if (Keyboard.current.dKey.isPressed) moveInput.x = 1;

        // Prevent calculating directions if no key is pressed (fixes vector normalization errors)
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = (freeMovingCamera.transform.forward * moveInput.y) + (freeMovingCamera.transform.right * moveInput.x);
            moveDirection.y = 0; // Lock movement horizontally

            freeMovingCamera.transform.position += moveDirection.normalized * freeMoveSpeed * Time.deltaTime;
        }
    }
}