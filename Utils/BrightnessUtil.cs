using UnityEngine;
using UnityEngine.UI;

public static class BrightnessUtil {
    /// <summary>
    /// 设置GameObject及其子物体的亮度
    /// </summary>
    /// <param name="obj">目标对象</param>
    /// <param name="factor">亮度系数 (1 = 原始亮度, 0.5 = 一半亮度, 0 = 全黑)</param>
    public static void SetBrightness(GameObject obj, float factor) {
        if (obj == null) return;

        // 1. UI 组件 (Image/Text/TMP等)
        foreach (var graphic in obj.GetComponentsInChildren<Graphic>(true)) {
            Color c = graphic.color;
            c = new Color(c.r * factor, c.g * factor, c.b * factor, c.a); // 只缩放 RGB
            graphic.color = c;
        }

        // 2. 普通 Renderer (MeshRenderer, SpriteRenderer等)
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) {
            foreach (var mat in renderer.materials) {
                Color c = mat.color;
                c = new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
                mat.color = c;

                // 如果有自发光 (Emission)，也缩放一下
                if (mat.HasProperty("_EmissionColor")) {
                    Color emission = mat.GetColor("_EmissionColor");
                    emission = new Color(emission.r * factor, emission.g * factor, emission.b * factor);
                    mat.SetColor("_EmissionColor", emission);
                }
            }
        }

        // 3. 粒子系统 (特效)
        foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true)) {
            var main = ps.main;
            Color c = main.startColor.color;
            c = new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
            main.startColor = c;
        }
    }
}
