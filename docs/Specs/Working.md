


Create a new layer named CharacterPreview (Inspector top-right Layer dropdown).



In Hierarchy, use GameObject > UMA > Create New Dynamic Character Avatar.

Rename it to UMAPreviewAvatar and set its layer to CharacterPreview (apply to children).

In the avatar Inspector, set Active Race to HumanMaleDCS (or HumanFemaleDCS), then click Build Character if needed.

Create a Camera named UMAPreviewCamera, position it to frame the UMA body.

Set UMAPreviewCamera Culling Mask to only CharacterPreview.

Create a Render Texture asset (for example RT_UMA_Preview) and assign it to UMAPreviewCamera Target Texture.

In CharacterCreatorPanel UI, add a RawImage named UMAPreviewImage and set its Texture to RT_UMA_Preview.

Add one Directional Light for preview and set that light to CharacterPreview layer only so your menu lighting does not affect it.