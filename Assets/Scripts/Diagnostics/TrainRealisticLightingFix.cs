using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public static class TrainRealisticLightingFix
{
    private const string SceneName = "TrainRealistic";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterSceneLoad()
    {
        ApplyIfTrainRealistic();
        SceneManager.sceneLoaded += (_, _) => ApplyIfTrainRealistic();
    }

    public static void ApplyIfTrainRealistic()
    {
        if (SceneManager.GetActiveScene().name != SceneName) return;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.62f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.48f, 0.45f, 0.4f);
        RenderSettings.ambientIntensity = 2f;
        RenderSettings.fogColor = new Color(0.52f, 0.56f, 0.62f);
        RenderSettings.fogDensity = 0.006f;

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                light.shadows = LightShadows.None;
                light.intensity = Mathf.Max(light.intensity, 2.5f);
                RenderSettings.sun = light;
            }
        }

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.52f, 0.56f, 0.62f);
        }

        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.drawInstanced = false;
            terrain.bakeLightProbesForTrees = false;

            if (!Application.isPlaying) continue;

            TerrainData sourceData = terrain.terrainData;
            if (sourceData == null) continue;

            TerrainData runtimeData = Object.Instantiate(sourceData);
            terrain.terrainData = runtimeData;

            TreeInstance[] trees = runtimeData.treeInstances;
            for (int i = 0; i < trees.Length; i++)
            {
                trees[i].color = Color.white;
                trees[i].lightmapColor = Color.white;
            }
            runtimeData.treeInstances = trees;

            DetailPrototype[] details = runtimeData.detailPrototypes;
            for (int i = 0; i < details.Length; i++)
            {
                details[i].healthyColor = Color.white;
                details[i].dryColor = new Color(0.75f, 0.75f, 0.7f);
            }
            runtimeData.detailPrototypes = details;
        }

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            renderer.receiveShadows = false;

            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (!Application.isPlaying) continue;

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                if (material.HasProperty("_ReceiveShadows"))
                {
                    material.SetFloat("_ReceiveShadows", 0f);
                }
            }
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void ApplyInEditor()
    {
        EditorApplication.delayCall += () =>
        {
            ApplyIfTrainRealistic();

            if (SceneManager.GetActiveScene().name == SceneName)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        };
    }
#endif
}
