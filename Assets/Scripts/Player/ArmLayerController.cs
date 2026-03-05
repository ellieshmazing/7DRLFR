using UnityEngine;

/// <summary>
/// Sits on the Arm pivot GO and owns direct references to the arm layer child transforms.
/// In Play mode: edit <see cref="config"/> armLayers in the Inspector and hit
/// "Apply Layers from Config" via the context menu to reposition/recolor live.
/// </summary>
public class ArmLayerController : MonoBehaviour
{
    [Tooltip("Populated by PlayerAssembler at spawn time")]
    public Transform[] layerTransforms;

    [Tooltip("Source config — edit armLayers here to preview changes")]
    public PlayerConfig config;

    const float SPRITE_PX = 16f;
    const float PPU       = 128f;

    // ------------------------------------------------------------------
    // Context-menu live update
    // ------------------------------------------------------------------

    [ContextMenu("Apply Layers from Config")]
    public void ApplyFromConfig()
    {
        if (config == null || layerTransforms == null) return;

        float pixelToWorld    = config.playerScale / SPRITE_PX;
        float spriteLocalScale = config.playerScale * PPU / SPRITE_PX;

        var defs  = config.armLayers;
        int count = Mathf.Min(layerTransforms.Length, defs != null ? defs.Length : 0);

        for (int i = 0; i < count; i++)
        {
            var t   = layerTransforms[i];
            var def = defs[i];
            if (t == null) continue;

            t.localPosition = (Vector3)(def.localOffset * pixelToWorld);
            t.localScale    = Vector3.one * spriteLocalScale;

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color        = def.color;
                sr.sortingOrder = def.sortingOrder;
                // Sprite swap intentionally excluded — adjust in config and respawn.
            }
        }

        Debug.Log($"[ArmLayerController] Applied {count} layer(s) from config.", this);
    }
}
