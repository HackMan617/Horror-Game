using UnityEngine;
using Game.Characters;

/// <summary>
/// In the overworld, recolors the master sheets to the saved CharacterLook and feeds the
/// resulting front/back frames into the 2.5D CharacterBillboardAnimator. Uses the long-hair
/// (Female) sheets when look.body == Female, otherwise the male sheets. With the default look
/// this reproduces the original red-shirt/blue-pants character.
/// </summary>
public class CharacterLookApplier : MonoBehaviour
{
    public Texture2D masterFront;
    public Texture2D masterBack;
    public Texture2D masterFrontLong;                       // female / long hair (front)
    public Texture2D masterBackLong;                        // female / long hair (back)
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
        bool female = look.body == BodyType.Female;
        Texture2D front = (female && masterFrontLong != null) ? masterFrontLong : masterFront;
        Texture2D back  = (female && masterBackLong  != null) ? masterBackLong  : masterBack;
        if (front != null)
            animator.frontFrames = CharacterPalette.Slice(CharacterPalette.Recolor(front, look), 32, 32, pixelsPerUnit, pivot);
        if (back != null)
            animator.backFrames = CharacterPalette.Slice(CharacterPalette.Recolor(back, look), 32, 32, pixelsPerUnit, pivot);
    }
}
