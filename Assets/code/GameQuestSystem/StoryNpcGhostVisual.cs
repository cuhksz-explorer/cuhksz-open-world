using UnityEngine;
using UnityEngine.Rendering;

namespace GameQuestSystem
{
    /// <summary>剧情 NPC 镜像/幻影外观：保留原材质，仅在运行时实例化并半透明化。</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class StoryNpcGhostVisual : MonoBehaviour
    {
        bool _applied;

        void OnEnable()
        {
            // 仅在 Play Mode 实例化材质，避免编辑器中 renderer.materials 泄漏
            if (!Application.isPlaying || _applied)
                return;
            TryApply();
        }

        void TryApply()
        {
            var profile = GetComponent<StoryNpcVisualProfile>();
            if (profile == null || !profile.applyGhostEffect || _applied)
                return;

            var visual = transform.Find(StoryNpcVisualProfile.VisualChildName);
            if (visual == null)
                return;

            if (ApplyToVisual(visual.gameObject, profile.ghostAlpha, profile.ghostTint))
                _applied = true;
        }

        public static bool ApplyToVisual(GameObject visualRoot, float alpha = 0.45f, Color? tint = null)
        {
            if (visualRoot == null || !Application.isPlaying)
                return false;

            Color ghostTint = tint ?? new Color(0.75f, 0.88f, 1f, 1f);
            bool changed = false;

            bool useShared = !Application.isPlaying;

            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var sources = renderer.sharedMaterials;
                if (sources == null || sources.Length == 0)
                    continue;

                Material[] instances;
                if (useShared)
                {
                    instances = new Material[sources.Length];
                    for (int i = 0; i < sources.Length; i++)
                    {
                        Material source = sources[i];
                        if (source == null || source.shader == null)
                            continue;
                        instances[i] = new Material(source);
                    }
                }
                else
                {
                    instances = renderer.materials;
                }

                for (int i = 0; i < instances.Length; i++)
                {
                    Material source = i < sources.Length ? sources[i] : null;
                    Material working = instances[i];
                    if (working == null || working.shader == null)
                    {
                        if (source == null || source.shader == null)
                            continue;
                        working = new Material(source);
                        instances[i] = working;
                    }

                    ConfigureTransparent(working, alpha, ghostTint);
                    changed = true;
                }

                if (useShared)
                    renderer.sharedMaterials = instances;
                else
                    renderer.materials = instances;
            }

            return changed;
        }

        static void ConfigureTransparent(Material material, float alpha, Color tint)
        {
            if (material == null || material.shader == null)
                return;

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (material.HasProperty("_BaseColor"))
            {
                Color c = material.GetColor("_BaseColor");
                c = Color.Lerp(c, tint, 0.25f);
                c.a = alpha;
                material.SetColor("_BaseColor", c);
            }

            if (material.HasProperty("_Color"))
            {
                Color c = material.GetColor("_Color");
                c = Color.Lerp(c, tint, 0.25f);
                c.a = alpha;
                material.SetColor("_Color", c);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 0.2f);
            }
        }
    }
}
