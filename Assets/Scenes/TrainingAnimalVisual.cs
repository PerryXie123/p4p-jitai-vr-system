using UnityEngine;

public class TrainingAnimalVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer[] renderers;
    private Material[][] rendererMaterials;
    private Vector3 originalScale;
    private float visibility = 1f;
    private bool isInitialized;

    public float Visibility => visibility;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void MoveVisibilityToward(float targetVisibility, float fadeSeconds)
    {
        EnsureInitialized();

        float speed = fadeSeconds <= 0f ? 1f : Time.deltaTime / fadeSeconds;
        SetVisibility(Mathf.MoveTowards(visibility, targetVisibility, speed));
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

        transform.localScale = originalScale * Mathf.Lerp(0.85f, 1f, visibility);
        ApplyAlpha(visibility);
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

        originalScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            rendererMaterials[i] = renderers[i] != null ? renderers[i].materials : null;
        }

        isInitialized = true;
    }
}
