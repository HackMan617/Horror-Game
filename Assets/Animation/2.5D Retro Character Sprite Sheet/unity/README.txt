CHARACTER PALETTE-SWAP — UNITY DROP-IN
======================================

WHAT'S HERE
  character_master.png        Front-facing master sheet (5 frames, 32x32). Eyes baked to magenta sentinel.
  character_master_back.png   Back-facing master sheet (5 frames, 32x32).
  CharacterPalette.cs         The recolor system + the selectable color tables (mirrors the Character Creator tool).
  CharacterAnimator.cs        Spawns the recolored character and runs the walk cycle. No Animator Controller needed.

THE IDEA
  One master sheet, recolored per character at spawn. You store only a CharacterLook
  (5 ints: hair, skin, eyes, shirt, pants) per player — never a generated image.
  At 1-2 characters the cost is two small texture copies; negligible.

  Eyes share black with the outline, so the master sheets bake the eye pixels to a
  magenta key (255,0,255). That makes eyes a normal color swap like everything else.

IMPORT SETTINGS (important — do this for BOTH .png files)
  Texture Type ............ Sprite (2D and UI)
  Sprite Mode ............. Single   (the script slices frames at runtime)
  Pixels Per Unit ......... 32       (match CharacterAnimator.pixelsPerUnit)
  Filter Mode ............. Point (no filter)
  Compression ............. None
  Read/Write Enabled ...... ON       (required — the swap reads pixels)
  Generate Mip Maps ....... OFF

SETUP
  1. Drop both .png and both .cs files into your project (e.g. Assets/Characters/).
  2. Create a GameObject, add a SpriteRenderer, add CharacterAnimator.
  3. Assign masterFront = character_master, masterBack = character_master_back.
  4. Set the `look` indices (or assign from your save data) and press Play.

DRIVING IT FROM YOUR MOVEMENT CODE
  Each frame, hand the animator your velocity:

      characterAnimator.SetMovement(rb.velocity);          // Rigidbody2D
      // or
      characterAnimator.SetMovement(moveInput * speed);    // direct input

  up    -> back sheet
  down  -> front sheet
  left  -> front sheet, mirrored (flipX)
  right -> front sheet
  (Left/right reuse the front art mirrored, since the character art only has
   front + back facings. Add dedicated side sheets later if you want true profiles.)

CHANGING THE LOOK AT RUNTIME (creation menu / wardrobe)
  Set the indices and rebuild:

      characterAnimator.look.hair  = 1;   // Blonde
      characterAnimator.look.eyes  = 2;   // Blue
      characterAnimator.Rebuild();

  The option order in code matches the Character Creator tool exactly, so an index
  saved from the tool maps to the same color here.

SAVING PER PLAYER
  CharacterLook is [System.Serializable] — write the 5 ints to PlayerPrefs, JSON,
  or your save system. That's the entire per-character footprint.

ADDING / EDITING COLORS
  Edit the Hair / Skin / Eyes / Shirt / Pants arrays in CharacterPalette.cs.
  Do NOT change the SkinBase/HairBase/... source constants — those are the exact
  pixel colors in the master sheets that the swap matches against.
