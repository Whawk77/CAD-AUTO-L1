# 咕嘎 look mechanics

咕嘎 is a tiny humanoid girl in a charcoal-black penguin onesie: black bobbed hair and bangs, large blue eyes with visible sclera/iris/pupil/highlight construction, a penguin hood with white eye patches and a yellow beak, rounded flipper sleeves, white belly, yellow webbed feet, and a low penguin tail. Preserve this exact anime/pixel-art-adjacent identity, face proportions, clothing construction, palette, crisp outline, and compact full-body scale.

## Natural motion

- Anchor both yellow feet, pelvis, lower torso, white belly, and tail baseline. The character must not slide, rotate, skew, rescale, or lean as a whole.
- The eyes lead. Rotate/redraw both complete eye surfaces coherently inside their original apertures: sclera, blue iris, dark pupil, eyelids, lashes, and highlights move as one gaze system. Never paste floating pupils or replacement eyes.
- The head and neck follow with a restrained yaw/pitch. The black bob, bangs, and penguin hood remain attached and preserve their shape; only small perspective/occlusion changes are allowed.
- The hood beak follows the skull as a rigid attached feature. It is a secondary directional landmark, never a detached arrow. The hood's two white eye patches remain part of the costume and do not become expressive replacement eyes.
- Flipper sleeves and upper torso may show a very small natural counter-shift. Keep the white belly frontal and stable, and keep the tail attached with only slight follow-through.
- No broad anatomical warp: do not stretch the skull, brows, mouth, hood, sleeves, hands, belly, feet, or tail.

## Cardinal pose families

- `000 up`: face broadly frontal; both blue irises/pupils rise visibly within the original eye apertures, upper lids open slightly, chin lifts a little, and the hood beak pitches subtly upward. Feet and belly remain fixed.
- `090 screen-right`: eyes and nose/mouth center shift unmistakably toward the viewer's screen-right side of the head; head yaws slightly right, revealing a little more screen-left cheek/hair and occluding a little screen-right cheek/hair. Hood and beak follow the skull.
- `180 down`: face broadly frontal; both irises/pupils lower visibly, upper lids lower slightly, chin tucks, and the hood beak pitches subtly downward without covering or deforming the face.
- `270 screen-left`: exact semantic inverse of `090`; eyes and nose/mouth center shift unmistakably toward the viewer's screen-left, head yaws slightly left, revealing a little more screen-right cheek/hair and occluding a little screen-left cheek/hair.

## Motion budget and continuity

Each 22.5-degree step changes the complete eye gaze first, then head yaw/pitch, eyelids, bangs/hood occlusion, and only a trace of upper-body follow-through by a roughly even amount. Horizontal turns must pass smoothly through the right and left cardinals; vertical turns must pass smoothly through up and down. Keep face size, pupil size, hood size, body height, planted feet, belly shape, and baseline constant. `157.5 -> 180` and `337.5 -> 000` must be single even steps with no snap, flip, scale pop, or identity drift.

## Forbidden shortcuts

No whole-sprite rotation, affine tilt, mirrored face, pupil-only stickers, googly eyes, new props, labels, arrows, guide marks, shadows, glows, scenery, detached effects, or cyan inside the pet.
