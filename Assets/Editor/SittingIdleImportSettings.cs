using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes "Assets/Prefabs/Sitting Idle.fbx" (Mixamo) usable on the humanoid boy NPC:
/// imports it as Humanoid, looping, with root transform baked so it never drifts.
/// </summary>
public class SittingIdleImportSettings : AssetPostprocessor
{
    private const string SittingIdlePath = "Assets/Prefabs/Sitting Idle.fbx";

    private void OnPreprocessModel()
    {
        if (assetPath != SittingIdlePath)
            return;

        ModelImporter importer = (ModelImporter)assetImporter;
        if (importer.animationType != ModelImporterAnimationType.Human)
            importer.animationType = ModelImporterAnimationType.Human;
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    }

    private void OnPreprocessAnimation()
    {
        if (assetPath != SittingIdlePath)
            return;

        ModelImporter importer = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }

        importer.clipAnimations = clips;
    }
}

