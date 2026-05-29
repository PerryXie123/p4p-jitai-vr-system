using UnityEngine;
using UnityEngine.UI;

public class FocusFeedbackController : MonoBehaviour
{
    [Header("Focus Source")]
    [SerializeField] private DataReceiverScript dataReceiver;

    [Header("Timing")]
    [SerializeField, Range(0.5f, 30f)] private float unfocusedDelay = 5f;
    [SerializeField, Range(0.1f, 10f)] private float fadeDuration = 1f;

    [Header("Screen Darkening")]
    [SerializeField] private CanvasGroup darkenOverlay;
    [SerializeField] private Graphic darkenGraphic;
    [SerializeField] private bool useRuntimeOverlayQuad = true;
    [SerializeField] private bool previewDarkening;
    [SerializeField, Range(0f, 1f)] private float darkenedAlpha = 0.65f;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool keepOverlayInFrontOfCamera = true;
    [SerializeField, Range(0.25f, 10f)] private float overlayDistance = 1.5f;
    [SerializeField, Range(1f, 3f)] private float overlaySizeMultiplier = 1.2f;

    [Header("Orb Brightness")]
    [SerializeField] private Renderer orbRenderer;
    [SerializeField] private Light orbLight;
    [SerializeField] private bool drawOrbOverDarkening = true;
    [SerializeField] private int orbOverlayRenderQueue = 3101;
    [SerializeField] private Color normalEmissionColor = Color.cyan;
    [SerializeField] private Color focusedEmissionColor = Color.cyan;
    [SerializeField, Range(0f, 10f)] private float normalEmissionIntensity = 1f;
    [SerializeField, Range(0f, 20f)] private float unfocusedEmissionIntensity = 5f;
    [SerializeField, Range(0f, 20f)] private float normalLightIntensity = 1f;
    [SerializeField, Range(0f, 50f)] private float unfocusedLightIntensity = 8f;

    private float unfocusedTimer;
    private float feedbackAmount;
    private MaterialPropertyBlock propertyBlock;
    private Transform runtimeOverlayTransform;
    private Renderer runtimeOverlayRenderer;
    private Material runtimeOverlayMaterial;
    private Material[] orbMaterials;
    private int[] originalOrbRenderQueues;

    private void Awake()
    {
        if (dataReceiver == null)
        {
            dataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        propertyBlock = new MaterialPropertyBlock();

        if (useRuntimeOverlayQuad)
        {
            CreateRuntimeOverlayQuad();
        }

        ConfigureOrbRenderOrder();

        if (darkenOverlay != null)
        {
            if (darkenGraphic == null)
            {
                darkenGraphic = darkenOverlay.GetComponent<Graphic>();
            }

            if (darkenGraphic != null)
            {
                Color color = darkenGraphic.color;
                color.a = 1f;
                darkenGraphic.color = color;
            }

            darkenOverlay.alpha = 0f;
            darkenOverlay.blocksRaycasts = false;
            darkenOverlay.interactable = false;
        }
    }

    private void OnDestroy()
    {
        RestoreOrbRenderOrder();
    }

    private void Update()
    {
        bool isFocused = dataReceiver != null && dataReceiver.IsFocused;

        if (isFocused)
        {
            unfocusedTimer = 0f;
        }
        else
        {
            unfocusedTimer += Time.deltaTime;
        }

        float targetAmount = previewDarkening || unfocusedTimer >= unfocusedDelay ? 1f : 0f;
        float step = fadeDuration <= 0f ? 1f : Time.deltaTime / fadeDuration;
        feedbackAmount = Mathf.MoveTowards(feedbackAmount, targetAmount, step);

        ApplyDarkening();
        ApplyOrbBrightness();
    }

    private void LateUpdate()
    {
        PositionOverlayInFrontOfCamera();
        PositionRuntimeOverlayInFrontOfCamera();
    }

    private void ApplyDarkening()
    {
        if (darkenOverlay != null)
        {
            if (darkenGraphic != null && darkenGraphic.color.a <= 0f)
            {
                Color color = darkenGraphic.color;
                color.a = 1f;
                darkenGraphic.color = color;
            }

            darkenOverlay.alpha = Mathf.Lerp(0f, darkenedAlpha, feedbackAmount);
        }

        if (runtimeOverlayMaterial != null)
        {
            Color color = Color.black;
            color.a = Mathf.Lerp(0f, darkenedAlpha, feedbackAmount);
            runtimeOverlayMaterial.color = color;
        }
    }

    private void PositionOverlayInFrontOfCamera()
    {
        if (!keepOverlayInFrontOfCamera || darkenOverlay == null || targetCamera == null) return;

        RectTransform overlayRect = darkenOverlay.GetComponent<RectTransform>();
        if (overlayRect == null) return;

        Transform cameraTransform = targetCamera.transform;
        Transform overlayTransform = darkenOverlay.transform;

        overlayTransform.position = cameraTransform.position + cameraTransform.forward * overlayDistance;
        overlayTransform.rotation = cameraTransform.rotation;

        float height = 2f * overlayDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * targetCamera.aspect;

        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.sizeDelta = new Vector2(width * overlaySizeMultiplier, height * overlaySizeMultiplier);
        overlayRect.localScale = Vector3.one;
    }

    private void CreateRuntimeOverlayQuad()
    {
        if (runtimeOverlayTransform != null) return;

        GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlay.name = "Runtime Darken Overlay";
        overlay.transform.SetParent(transform, false);

        Collider overlayCollider = overlay.GetComponent<Collider>();
        if (overlayCollider != null)
        {
            Destroy(overlayCollider);
        }

        runtimeOverlayTransform = overlay.transform;
        runtimeOverlayRenderer = overlay.GetComponent<Renderer>();
        runtimeOverlayMaterial = CreateOverlayMaterial();
        runtimeOverlayRenderer.sharedMaterial = runtimeOverlayMaterial;
        runtimeOverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        runtimeOverlayRenderer.receiveShadows = false;
        runtimeOverlayRenderer.enabled = true;
    }

    private Material CreateOverlayMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.name = "Runtime Darken Overlay Material";
        material.color = new Color(0f, 0f, 0f, 0f);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void PositionRuntimeOverlayInFrontOfCamera()
    {
        if (!useRuntimeOverlayQuad || runtimeOverlayTransform == null || targetCamera == null) return;

        Transform cameraTransform = targetCamera.transform;
        runtimeOverlayTransform.position = cameraTransform.position + cameraTransform.forward * overlayDistance;
        runtimeOverlayTransform.rotation = cameraTransform.rotation;

        float height = 2f * overlayDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * targetCamera.aspect;
        runtimeOverlayTransform.localScale = new Vector3(
            width * overlaySizeMultiplier,
            height * overlaySizeMultiplier,
            1f);
    }

    private void ApplyOrbBrightness()
    {
        Color emissionColor = Color.Lerp(
            normalEmissionColor * normalEmissionIntensity,
            focusedEmissionColor * unfocusedEmissionIntensity,
            feedbackAmount);

        if (orbRenderer != null)
        {
            orbRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            orbRenderer.SetPropertyBlock(propertyBlock);
        }

        if (orbLight != null)
        {
            orbLight.intensity = Mathf.Lerp(normalLightIntensity, unfocusedLightIntensity, feedbackAmount);
        }
    }

    private void ConfigureOrbRenderOrder()
    {
        if (!drawOrbOverDarkening || orbRenderer == null) return;

        orbMaterials = orbRenderer.materials;
        originalOrbRenderQueues = new int[orbMaterials.Length];

        for (int i = 0; i < orbMaterials.Length; i++)
        {
            originalOrbRenderQueues[i] = orbMaterials[i].renderQueue;
            orbMaterials[i].renderQueue = orbOverlayRenderQueue;
        }
    }

    private void RestoreOrbRenderOrder()
    {
        if (orbMaterials == null || originalOrbRenderQueues == null) return;

        for (int i = 0; i < orbMaterials.Length && i < originalOrbRenderQueues.Length; i++)
        {
            if (orbMaterials[i] != null)
            {
                orbMaterials[i].renderQueue = originalOrbRenderQueues[i];
            }
        }
    }
}
