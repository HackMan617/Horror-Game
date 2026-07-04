# Compass Sprite — Unity 2D

A vintage cartography compass sprite with full 32-frame rotation cycle. The dial shows aged parchment with brass cardinal rose, N/S/E/W letters in player shirt-red (#c05a4a), and a rotating needle with sage-green counterweight.

## Sprite Sheet

**File:** `sprites/compass_spritesheet.png`  
**Size:** 80×80 px per frame  
**Frames:** 32 (0° to 360°, 11.25° increments)  
**Grid:** 8 columns × 4 rows  
**Layout:** Left-to-right, top-to-bottom

### Import Settings

1. **Texture Type:** Sprite (2D and UI)
2. **Sprite Mode:** Multiple
3. **Pixels Per Unit:** 100
4. **Filter Mode:** Point (no filter)
5. **Compression:** None (or Lossless for smaller builds)

### Slice Configuration

In Unity's Sprite Editor:

- **Sprite Size:** 80×80 px
- **Column:** 8
- **Row:** 4
- **Offset X:** 0
- **Offset Y:** 0

Unity will auto-generate 32 sprites named `compass_spritesheet_0` through `compass_spritesheet_31`.

### Frame-to-Angle Mapping

```csharp
// Calculate frame index from heading angle (degrees: 0-360)
int frameIndex = Mathf.RoundToInt((angle / 360f) * 32) % 32;

// Get sprite by index
Sprite frame = spriteArray[frameIndex];
spriteRenderer.sprite = frame;
```

### Color Palette

| Element | Hex | RGB | Notes |
|---------|-----|-----|-------|
| Dial base | #f4ede5 | 244, 237, 229 | Aged parchment highlight |
| Dial shadow | #d4c4b0 | 212, 196, 176 | Parchment edge |
| Cardinal rose | #c68c5a | 198, 140, 90 | Ornamental brass |
| Needle (north) | #c05a4a | 192, 90, 74 | Player shirt red |
| Counterweight | #7ab0a0 | 122, 176, 160 | Sage green accent |
| Pin (brass) | #c8952e | 200, 149, 46 | Metallic center |
| Glow | #7ab0a0 | 122, 176, 160 | Soft sage outline |

## Usage in Unity

### Setup in Hierarchy

1. Create an empty GameObject: `CompassDisplay`
2. Add a SpriteRenderer component
3. Load the sliced sprite sheet

### C# Script Example

```csharp
using UnityEngine;

public class CompassDisplay : MonoBehaviour
{
    [SerializeField] private Sprite[] compassFrames; // Assign sliced sprites here
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    private float currentHeading = 0f;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    public void SetHeading(float angle)
    {
        currentHeading = Mathf.Repeat(angle, 360f);
        UpdateFrame();
    }
    
    private void UpdateFrame()
    {
        int frameIndex = Mathf.RoundToInt((currentHeading / 360f) * 32) % 32;
        spriteRenderer.sprite = compassFrames[frameIndex];
    }
}
```

### Animation (Animator)

1. Create an Animator Controller: `CompassAnimator`
2. Add an Animation Clip: `CompassSpin`
3. Set animation speed to taste (e.g., 10 FPS for subtle drift)
4. Add parameter: `Heading` (float)

### Direction Mapping

```
0°   = North (top)
90°  = East (right)
180° = South (bottom)
270° = West (left)
```

## Design Notes

- **Dial:** Fixed in place. Aged parchment gradient with vignette shadow.
- **Cardinal Rose:** Ornamental brass-colored floral points at N/S/E/W.
- **Letters:** Player palette red (shirt color) for cardinal directions.
- **Needle:** Sharp point facing north (0°), sage-green counterweight at south.
- **Pin:** Brass-colored center with metallic highlight.
- **Glow:** Subtle sage-green aura around dial edge.

## Integration Checklist

- [ ] Import `compass_spritesheet.png` with Sprite (2D and UI) texture type
- [ ] Use Sprite Editor to slice into 32 frames (8×4 grid, 80×80 px each)
- [ ] Create SpriteRenderer GameObject and assign sprite array
- [ ] Use script to map heading angle → frame index
- [ ] Test frame updates with `SetHeading(angle)` calls
- [ ] Adjust Pixels Per Unit if sprite appears too large/small in scene
