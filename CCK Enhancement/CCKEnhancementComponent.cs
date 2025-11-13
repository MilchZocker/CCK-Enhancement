using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

public enum AdvAvatarPropertyType { Bool, Float, Int, Color }

// Only include CVR Settings Types that support animation clips
public enum CVRSettingsType 
{ 
    Toggle,        // Bool on/off with animation clips
    Slider,        // Float slider with min/max animation clips
    Dropdown,      // Int dropdown with multiple animation clips
    Color          // Color picker (only for actual color selection, not animation)
}

[Serializable]
public class AnimatorMergeData
{
#if UNITY_EDITOR
    public List<AnimatorController> animators = new List<AnimatorController>();
#endif
}

[Serializable]
public class MaterialTargetObject
{
    public GameObject gameObject;
    public bool enabled = true;
}

[Serializable]
public class PropertyAnimGenerationData
{
    public GameObject targetObject;
    public Material targetMaterial;
    public List<MaterialTargetObject> materialTargetObjects = new List<MaterialTargetObject>();
    public string serializedProperty;
    public string advAvatarPropertyName;
    public AdvAvatarPropertyType advAvatarPropertyType;
    public CVRSettingsType cvrSettingsType = CVRSettingsType.Toggle;
    public bool reverseGeneration = false;
    public bool useMaterialProperty = false;
    
    // For color properties
    public Color colorValue0 = Color.black;
    public Color colorValue1 = Color.white;
    
    // For float/int properties
    public float floatValue0 = 0f;
    public float floatValue1 = 1f;
    public int intValue0 = 0;
    public int intValue1 = 1;
    
    // For dropdown
    public int dropdownOptionCount = 2;
    public List<string> dropdownOptionNames = new List<string> { "Option 0", "Option 1" };
}

[Serializable]
public class MultiClipMergeData
{
    public string advAvatarPropertyName;
    public AdvAvatarPropertyType advAvatarPropertyType;
    public List<AnimationClip> animationClips = new List<AnimationClip>();
}

[AddComponentMenu("CVR Tools/CCK Enhancement Component")]
public class CCKEnhancementComponent : MonoBehaviour
{
    public GameObject avatarRoot;
    public AnimatorMergeData animatorMergeData = new AnimatorMergeData();
    public List<PropertyAnimGenerationData> propertyAnimGenData = new List<PropertyAnimGenerationData>();
    public List<MultiClipMergeData> multiClipMergeData = new List<MultiClipMergeData>();
}
