using UnityEngine;
using Game.Characters;

/// <summary>
/// In the overworld, recolors the master sheets to the saved CharacterLook and feeds
/// the resulting front/back frames into the 2.5D CharacterBillboardAnimator, so the
/// in-game player matches the character chosen on the select screen. With the default
/// look this reproduces the original red-shirt/blue-pants character.
/// </summary>
public class CharacterLookApplier : MonoBehaviour
{
    public Texture2D masterFront;
    public Texture2D masterBack;
    public CharacterBillboardAnimator animator;
    public float pixelsPerUnit = 16f;                       // matches the billboard character scale
    public Vector2 pivot = new Vector2(0.5f, 0.09f);        // feet, bottom-centre

    void Awake()
    {
        if (animator == null) animator = GetComponent<CharacterBillboardAnimator>();
        Apply(CharacterStore.Load());
    }

    public void Apply(CharacterLook look)
    {
        if (animator == null) return;
        if (masterFront != null)
        {
            var tex = CharacterPalette.Recolor(masterFront, look);
            animator.frontFrames = CharacterPalette.Slice(tex, 32, 32, pixelsPerUnit, pivot);
        }
        if (masterBack != null)
        {
            var tex = CharacterPalette.Recolor(masterBack, look);
            animator.backFrames = CharacterPalette.Slice(tex, 32, 32, pixelsPerUnit, pivot);
        }
    }
}
