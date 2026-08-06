using UnityEngine;
using UnityEngine.Rendering;

public class TrainingAnimalVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer[] renderers;
    private Material[][] rendererMaterials;
    private Transform[] hierarchyTransforms;
    private int[] originalLayers;
    private Vector3 startingLocalPosition;
    private Quaternion startingLocalRotation;
    private Vector3 originalScale;
    private float visibility = 1f;
    private bool isFadingIn;
    private bool isInitialized;

    public float Visibility => visibility;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        RestoreOriginalLayers();
    }

    public void MoveVisibilityToward(
        float targetVisibility,
        float fadeSeconds,
        float fadeInRotationVariation = 0f)
    {
        EnsureInitialized();

        bool shouldFadeIn = targetVisibility > visibility;
        if (shouldFadeIn && !isFadingIn && fadeInRotationVariation > 0f)
        {
            float yawOffset = Random.Range(-fadeInRotationVariation, fadeInRotationVariation);
            transform.localRotation *= Quaternion.Euler(0f, yawOffset, 0f);
        }

        float speed = fadeSeconds <= 0f ? 1f : Time.deltaTime / fadeSeconds;
        SetVisibility(Mathf.MoveTowards(visibility, targetVisibility, speed));
        isFadingIn = targetVisibility > visibility;
    }

    public void SetVisibility(float value)
    {
        EnsureInitialized();

        visibility = Mathf.Clamp01(value);
        bool shouldBeActive = visibility > 0.001f;

        if (gameObject.activeSelf != shouldBeActive)
        {
            gameObject.SetActive(shouldBeActive);
        }

        transform.localScale = originalScale;
        ApplyAlpha(visibility);
    }

    public void ResetToStartingPosition()
    {
        EnsureInitialized();
        transform.SetLocalPositionAndRotation(startingLocalPosition, startingLocalRotation);
    }

    public void CopyStartingPoseFrom(TrainingAnimalVisual source)
    {
        if (source == null) return;

        source.EnsureInitialized();
        EnsureInitialized();

        startingLocalPosition = source.startingLocalPosition;
        startingLocalRotation = source.startingLocalRotation;
        originalScale = source.originalScale;
        ResetToStartingPosition();
        transform.localScale = originalScale;
    }

    private void ApplyAlpha(float alpha)
    {
        if (rendererMaterials == null) return;

        foreach (Material[] materials in rendererMaterials)
        {
            if (materials == null) continue;

            foreach (Material material in materials)
            {
                SetMaterialAlpha(material, alpha);
            }
        }
    }

    private static void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null) return;

        if (material.HasProperty(BaseColorId))
        {
            Color color = material.GetColor(BaseColorId);
            color.a = alpha;
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            Color color = material.GetColor(ColorId);
            color.a = alpha;
            material.SetColor(ColorId, color);
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        startingLocalPosition = transform.localPosition;
        startingLocalRotation = transform.localRotation;
        originalScale = transform.localScale;
        MoveHierarchyToIgnoreRaycastLayer();
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            rendererMaterials[i] = renderers[i] != null ? renderers[i].materials : null;

            if (rendererMaterials[i] == null) continue;

            foreach (Material material in rendererMaterials[i])
            {
                ConfigureForTransparency(material);
            }
        }

        isInitialized = true;
    }

    private void MoveHierarchyToIgnoreRaycastLayer()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer < 0)
        {
            Debug.LogWarning("The Ignore Raycast layer is unavailable; animals may block eye gaze.", this);
            return;
        }

        hierarchyTransforms = GetComponentsInChildren<Transform>(true);
        originalLayers = new int[hierarchyTransforms.Length];

        for (int i = 0; i < hierarchyTransforms.Length; i++)
        {
            GameObject hierarchyObject = hierarchyTransforms[i].gameObject;
            originalLayers[i] = hierarchyObject.layer;
            hierarchyObject.layer = ignoreRaycastLayer;
        }
    }

    private void RestoreOriginalLayers()
    {
        if (hierarchyTransforms == null || originalLayers == null) return;

        for (int i = 0; i < hierarchyTransforms.Length && i < originalLayers.Length; i++)
        {
            if (hierarchyTransforms[i] != null)
            {
                hierarchyTransforms[i].gameObject.layer = originalLayers[i];
            }
        }
    }

    private static void ConfigureForTransparency(Material material)
    {
        if (material == null) return;

        // URP/Lit ignores colour alpha while the material is opaque. Renderer.materials
        // gives us instances, so these changes do not affect the shared material asset.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 1f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return;
        }

        // Built-in Standard shader fallback.
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
