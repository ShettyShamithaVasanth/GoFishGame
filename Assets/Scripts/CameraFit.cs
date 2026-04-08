using Sirenix.OdinInspector;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Camera field of view adjustment system optimized for various aspect ratios.
    /// Provides special handling for 8:5 portrait displays to prevent UI overlap.
    /// </summary>
    public class CameraFit : MonoBehaviour
    {
        [Header("Target Configuration")]
        [Tooltip("Target aspect ratio (width/height). Default 9/16 = 0.5625 for portrait")]
        public float targetAspect = 9f / 16f;

        [Range(0, 1)]
        [Tooltip("Power applied to aspect ratio difference. Lower values = gentler adjustment")]
        public float power = 0.5f;

        [Header("8:5 Portrait Optimization")]
        [Tooltip("Special power value for 8:5 portrait displays to prevent UI overlap")]
        [Range(0, 1)]
        [SerializeField] private float portrait8x5Power = 0.3f;

        [Tooltip("Minimum FOV multiplier to prevent excessive reduction")]
        [Range(0.5f, 1f)]
        [SerializeField] private float minFOVMultiplier = 0.85f;

        [Tooltip("Tolerance for detecting 8:5 portrait aspect ratio (0.625)")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float portrait8x5Tolerance = 0.05f;

        [Header("Debug Information")]
        [SerializeField, ReadOnly] private float currentAspectRatio;
        [SerializeField, ReadOnly] private float calculatedRatio;
        [SerializeField, ReadOnly] private float appliedPower;
        [SerializeField, ReadOnly] private float finalFOVMultiplier;
        [SerializeField, ReadOnly] private bool is8x5PortraitMode;

        private float originalSize;

        private float originalFOV;
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (cam == null)
            {
                Debug.LogError("CameraFit requires Camera component!");
                return;
            }

            // ⭐ SUPPORT BOTH MODES
            if (cam.orthographic)
            {
                originalSize = cam.orthographicSize;
            }
            else
            {
                originalFOV = cam.fieldOfView;
            }
        }

        private void Start()
        {
            HandleAspectRatio();
        }

        [Button("Handle Current Aspect Ratio")]
        private void HandleAspectRatio()
        {
            Rect safeArea = Screen.safeArea;
            currentAspectRatio = safeArea.height != 0 ? safeArea.width / safeArea.height : targetAspect;

            calculatedRatio = targetAspect / currentAspectRatio;

            // Detect 8:5 portrait mode (aspect ratio ≈ 0.625)
            is8x5PortraitMode = Mathf.Abs(currentAspectRatio - 0.625f) < portrait8x5Tolerance;

            // Use adaptive power based on aspect ratio
            appliedPower = is8x5PortraitMode ? portrait8x5Power : power;

            // Apply power to the ratio
            float poweredRatio = Mathf.Pow(calculatedRatio, appliedPower);

            // Prevent excessive FOV reduction to avoid UI overlap
            finalFOVMultiplier = Mathf.Max(poweredRatio, minFOVMultiplier);

            if (cam.orthographic)
            {
                cam.orthographicSize = originalSize * finalFOVMultiplier;
            }
            else
            {
                cam.fieldOfView = originalFOV * finalFOVMultiplier;
            }

            // Debug logging
            if (cam.orthographic)
            {
                Debug.Log($"[CameraFit] Aspect: {currentAspectRatio:F3}, " +
                          $"Size Multiplier: {finalFOVMultiplier:F3}, " +
                          $"Final Size: {cam.orthographicSize:F2}");
            }
            else
            {
                Debug.Log($"[CameraFit] Aspect: {currentAspectRatio:F3}, " +
                          $"FOV Multiplier: {finalFOVMultiplier:F3}, " +
                          $"Final FOV: {cam.fieldOfView:F1}");
            }
        }

        [Button("Test 8:5 Portrait (0.625)")]
        private void Test8x5Portrait()
        {
            TestAspectRatio(0.625f, "8:5 Portrait");
        }

        [Button("Test 9:16 Portrait (0.5625)")]
        private void Test9x16Portrait()
        {
            TestAspectRatio(0.5625f, "9:16 Portrait");
        }

        [Button("Test 16:9 Landscape (1.777)")]
        private void Test16x9Landscape()
        {
            TestAspectRatio(1.777f, "16:9 Landscape");
        }

        [Button("Test Current Screen Ratio")]
        private void TestCurrentScreenRatio()
        {
            float screenAspect = Screen.width / (float)Screen.height;
            TestAspectRatio(screenAspect, $"Current Screen ({Screen.width}x{Screen.height})");
        }

        private void TestAspectRatio(float testAspect, string description)
        {
            // Temporarily override current aspect for testing
            float originalCurrentAspect = currentAspectRatio;
            currentAspectRatio = testAspect;

            calculatedRatio = targetAspect / currentAspectRatio;
            is8x5PortraitMode = Mathf.Abs(currentAspectRatio - 0.625f) < portrait8x5Tolerance;
            appliedPower = is8x5PortraitMode ? portrait8x5Power : power;

            float poweredRatio = Mathf.Pow(calculatedRatio, appliedPower);
            finalFOVMultiplier = Mathf.Max(poweredRatio, minFOVMultiplier);

            float testFOV = originalFOV * finalFOVMultiplier;

            if (cam.orthographic)
            {
                Debug.Log($"[CameraFit TEST] {description}: Aspect {testAspect:F3}, " +
                          $"Size Multiplier: {finalFOVMultiplier:F3}, " +
                          $"Final Size: {(originalSize * finalFOVMultiplier):F2}");
            }
            else
            {
                Debug.Log($"[CameraFit TEST] {description}: Aspect {testAspect:F3}, " +
                          $"FOV Multiplier: {finalFOVMultiplier:F3}, Final FOV: {testFOV:F1}°");
            }

            if (cam.orthographic)
            {
                cam.orthographicSize = originalSize * finalFOVMultiplier;
            }
            else
            {
                cam.fieldOfView = testFOV;
            }

            // Restore original aspect for debug display
            currentAspectRatio = originalCurrentAspect;
        }

        [Button("Reset to Original FOV")]
        private void ResetToOriginalFOV()
        {
            if (cam.orthographic)
            {
                cam.orthographicSize = originalSize;
            }
            else
            {
                cam.fieldOfView = originalFOV;
            }
            Debug.Log($"[CameraFit] Reset to original FOV: {originalFOV:F1}°");
        }
    }
}