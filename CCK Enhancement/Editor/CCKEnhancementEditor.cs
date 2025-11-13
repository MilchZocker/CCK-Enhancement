#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using ABI.CCK.Scripts;
using ABI.CCK.Components;

// Editor Window
public class CCKEnhancementEditorWindow : EditorWindow
{
    private GameObject avatarRoot;
    private AnimatorMergeData animatorMergeData = new AnimatorMergeData();
    private List<PropertyAnimGenerationData> propertyAnimGenData = new List<PropertyAnimGenerationData>();
    private List<MultiClipMergeData> multiClipMergeData = new List<MultiClipMergeData>();
    private Vector2 scroll;
    private Dictionary<GameObject, List<string>> animPropCache = new Dictionary<GameObject, List<string>>();
    private Dictionary<Material, List<string>> materialPropCache = new Dictionary<Material, List<string>>();
    private Dictionary<Material, Dictionary<string, ShaderUtil.ShaderPropertyType>> materialPropTypes = new Dictionary<Material, Dictionary<string, ShaderUtil.ShaderPropertyType>>();

    [MenuItem("CVR Tools/CCK Enhancement")]
    public static void ShowWindow() => GetWindow<CCKEnhancementEditorWindow>("CCK Enhancement");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1) Merge Multiple Animators", EditorStyles.boldLabel);
        DrawAnimatorMergeUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2) Generate Property Animations + Avatar Entry", EditorStyles.boldLabel);
        DrawPropertyAnimationGenerationUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3) Merge Multiple Animation Clips to One Avatar Entry", EditorStyles.boldLabel);
        DrawMultiClipMergeUI();

        EditorGUILayout.EndScrollView();
    }

    void DrawAnimatorMergeUI()
    {
        int remIdx = -1;
        for (int i = 0; i < animatorMergeData.animators.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            animatorMergeData.animators[i] = (AnimatorController)EditorGUILayout.ObjectField(animatorMergeData.animators[i], typeof(AnimatorController), false);
            if (GUILayout.Button("X", GUILayout.Width(20))) remIdx = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remIdx >= 0) animatorMergeData.animators.RemoveAt(remIdx);

        if (GUILayout.Button("Add Animator Controller"))
            animatorMergeData.animators.Add(null);

        if (GUILayout.Button("Merge Animators"))
        {
            if (CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(avatarRoot, animatorMergeData.animators))
                CCKEnhancementEditorHelpers.PerformAnimatorMerge(animatorMergeData.animators, avatarRoot);
        }
    }

    void DrawPropertyAnimationGenerationUI()
    {
        int remIdx = -1;
        for (int i = 0; i < propertyAnimGenData.Count; i++)
        {
            var entry = propertyAnimGenData[i];
            EditorGUILayout.BeginVertical("box");

            entry.useMaterialProperty = EditorGUILayout.Toggle("Use Material Property", entry.useMaterialProperty);

            if (entry.useMaterialProperty)
            {
                entry.targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", entry.targetMaterial, typeof(Material), false);

                if (entry.targetMaterial != null)
                {
                    if (GUILayout.Button("Find Objects with Material"))
                    {
                        var foundObjects = FindObjectsWithMaterial(avatarRoot, entry.targetMaterial);
                        entry.materialTargetObjects = foundObjects.Select(go => new MaterialTargetObject { gameObject = go, enabled = true }).ToList();
                        Debug.Log($"Found {entry.materialTargetObjects.Count} objects with material '{entry.targetMaterial.name}'");
                    }

                    if (entry.materialTargetObjects.Count > 0)
                    {
                        EditorGUILayout.LabelField($"Objects with material ({entry.materialTargetObjects.Count}):", EditorStyles.boldLabel);
                        
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Select All", GUILayout.Width(100)))
                        {
                            foreach (var obj in entry.materialTargetObjects)
                                obj.enabled = true;
                        }
                        if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                        {
                            foreach (var obj in entry.materialTargetObjects)
                                obj.enabled = false;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space(5);

                        for (int j = 0; j < entry.materialTargetObjects.Count; j++)
                        {
                            var matTarget = entry.materialTargetObjects[j];
                            EditorGUILayout.BeginHorizontal();
                            matTarget.enabled = EditorGUILayout.Toggle(matTarget.enabled, GUILayout.Width(20));
                            EditorGUILayout.ObjectField(matTarget.gameObject, typeof(GameObject), true);
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.Space(5);

                    if (!materialPropCache.ContainsKey(entry.targetMaterial))
                        CacheMaterialProps(entry.targetMaterial);
                    var props = materialPropCache[entry.targetMaterial];
                    if (props.Count > 0)
                    {
                        int selIdx = string.IsNullOrEmpty(entry.serializedProperty) ? 0 : props.IndexOf(entry.serializedProperty);
                        selIdx = Mathf.Clamp(selIdx, 0, props.Count - 1);
                        selIdx = EditorGUILayout.Popup("Material Property", selIdx, props.ToArray());
                        entry.serializedProperty = props[selIdx];

                        if (!string.IsNullOrEmpty(entry.serializedProperty))
                        {
                            string propName = entry.serializedProperty.Split(new[] { ' ' }, 2)[0].Replace("material.", "");
                            if (materialPropTypes.ContainsKey(entry.targetMaterial) && 
                                materialPropTypes[entry.targetMaterial].ContainsKey(propName))
                            {
                                var propType = materialPropTypes[entry.targetMaterial][propName];
                                if (propType == ShaderUtil.ShaderPropertyType.Color)
                                    entry.advAvatarPropertyType = AdvAvatarPropertyType.Color;
                                else
                                    entry.advAvatarPropertyType = AdvAvatarPropertyType.Float;
                            }
                        }
                    }
                    else EditorGUILayout.LabelField("No animatable properties found.");
                }
                else EditorGUILayout.LabelField("Select Target Material first.");
            }
            else
            {
                entry.targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", entry.targetObject, typeof(GameObject), true);

                if (entry.targetObject != null)
                {
                    if (!animPropCache.ContainsKey(entry.targetObject))
                        CacheAnimProps(entry.targetObject);
                    var props = animPropCache[entry.targetObject];
                    if (props.Count > 0)
                    {
                        int selIdx = string.IsNullOrEmpty(entry.serializedProperty) ? 0 : props.IndexOf(entry.serializedProperty);
                        selIdx = Mathf.Clamp(selIdx, 0, props.Count - 1);
                        selIdx = EditorGUILayout.Popup("Serialized Property", selIdx, props.ToArray());
                        entry.serializedProperty = props[selIdx];
                    }
                    else EditorGUILayout.LabelField("No animatable properties found.");
                }
                else EditorGUILayout.LabelField("Select Target Object first.");
            }

            entry.advAvatarPropertyName = EditorGUILayout.TextField("Advanced Avatar Property Name", entry.advAvatarPropertyName);
            
            // Show property type selector
            if (!entry.useMaterialProperty)
                entry.advAvatarPropertyType = (AdvAvatarPropertyType)EditorGUILayout.EnumPopup("Property Type", entry.advAvatarPropertyType);
            else
                EditorGUILayout.LabelField("Property Type (Auto-detected)", entry.advAvatarPropertyType.ToString());
            
            // CVR Settings Type selection
            EditorGUILayout.LabelField("CVR Advanced Settings Type", EditorStyles.boldLabel);
            entry.cvrSettingsType = (CVRSettingsType)EditorGUILayout.EnumPopup("Settings Type", entry.cvrSettingsType);

            // Show appropriate value selectors based on CVR settings type and property type
            if (entry.cvrSettingsType == CVRSettingsType.Color)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Default Color", entry.colorValue0);
                    EditorGUILayout.HelpBox("Color type creates a color picker in-game. No animation clips generated.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Color settings type only works with Color property types.", MessageType.Warning);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Dropdown)
            {
                entry.dropdownOptionCount = EditorGUILayout.IntField("Number of Options", Mathf.Max(2, entry.dropdownOptionCount));
                
                while (entry.dropdownOptionNames.Count < entry.dropdownOptionCount)
                    entry.dropdownOptionNames.Add($"Option {entry.dropdownOptionNames.Count}");
                while (entry.dropdownOptionNames.Count > entry.dropdownOptionCount)
                    entry.dropdownOptionNames.RemoveAt(entry.dropdownOptionNames.Count - 1);
                
                EditorGUILayout.LabelField("Dropdown Options:", EditorStyles.boldLabel);
                for (int j = 0; j < entry.dropdownOptionNames.Count; j++)
                {
                    entry.dropdownOptionNames[j] = EditorGUILayout.TextField($"  Option {j}", entry.dropdownOptionNames[j]);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Slider)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Color Value 0 (Min)", entry.colorValue0);
                    entry.colorValue1 = EditorGUILayout.ColorField("Color Value 1 (Max)", entry.colorValue1);
                }
                else
                {
                    entry.floatValue0 = EditorGUILayout.FloatField("Float Value 0 (Min)", entry.floatValue0);
                    entry.floatValue1 = EditorGUILayout.FloatField("Float Value 1 (Max)", entry.floatValue1);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Toggle)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Bool)
                {
                    EditorGUILayout.LabelField("Bool Value 0: False, Bool Value 1: True");
                    entry.reverseGeneration = EditorGUILayout.Toggle("Reverse Generation (Swap False/True)", entry.reverseGeneration);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                {
                    entry.floatValue0 = EditorGUILayout.FloatField("Float Value 0 (Off)", entry.floatValue0);
                    entry.floatValue1 = EditorGUILayout.FloatField("Float Value 1 (On)", entry.floatValue1);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Int)
                {
                    entry.intValue0 = EditorGUILayout.IntField("Int Value 0 (Off)", entry.intValue0);
                    entry.intValue1 = EditorGUILayout.IntField("Int Value 1 (On)", entry.intValue1);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Color Value 0 (Off)", entry.colorValue0);
                    entry.colorValue1 = EditorGUILayout.ColorField("Color Value 1 (On)", entry.colorValue1);
                }
            }

            if (GUILayout.Button("Remove Entry"))
                remIdx = i;

            EditorGUILayout.EndVertical();
        }
        if (remIdx >= 0) propertyAnimGenData.RemoveAt(remIdx);

        if (GUILayout.Button("Add Property Animation Entry"))
            propertyAnimGenData.Add(new PropertyAnimGenerationData());

        if (GUILayout.Button("Generate Animations + Avatar Entries"))
        {
            if (!CCKEnhancementEditorHelpers.ValidateAvatar(avatarRoot)) return;
            if (propertyAnimGenData.Count == 0)
                EditorUtility.DisplayDialog("Error", "Add at least one property animation entry", "OK");
            else
                CCKEnhancementEditorHelpers.GeneratePropertyAnimationsWithAvatarEntries(avatarRoot, propertyAnimGenData);
        }
    }

    void DrawMultiClipMergeUI()
    {
        int remIdx = -1;
        for (int i = 0; i < multiClipMergeData.Count; i++)
        {
            var entry = multiClipMergeData[i];
            EditorGUILayout.BeginVertical("box");

            entry.advAvatarPropertyName = EditorGUILayout.TextField("Advanced Avatar Property Name", entry.advAvatarPropertyName);
            entry.advAvatarPropertyType = (AdvAvatarPropertyType)EditorGUILayout.EnumPopup("Advanced Avatar Property Type", entry.advAvatarPropertyType);

            int remClipIdx = -1;
            EditorGUILayout.LabelField("Animation Clips");
            for (int j = 0; j < entry.animationClips.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                entry.animationClips[j] = (AnimationClip)EditorGUILayout.ObjectField(entry.animationClips[j], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    remClipIdx = j;
                EditorGUILayout.EndHorizontal();
            }
            if (remClipIdx >= 0) entry.animationClips.RemoveAt(remClipIdx);

            if (GUILayout.Button("Add Animation Clip"))
                entry.animationClips.Add(null);

            if (GUILayout.Button("Remove Entry"))
                remIdx = i;

            EditorGUILayout.EndVertical();
        }
        if (remIdx >= 0) multiClipMergeData.RemoveAt(remIdx);

        if (GUILayout.Button("Add Multi Clip Merge Entry"))
            multiClipMergeData.Add(new MultiClipMergeData());

        if (GUILayout.Button("Merge Clips to Avatar Entries"))
        {
            if (!CCKEnhancementEditorHelpers.ValidateAvatar(avatarRoot)) return;
            if (multiClipMergeData.Count == 0)
                EditorUtility.DisplayDialog("Error", "Add at least one merging entry", "OK");
            else
                CCKEnhancementEditorHelpers.MergeMultipleClipsToAvatarEntries(avatarRoot, multiClipMergeData);
        }
    }

    void CacheAnimProps(GameObject target)
    {
        var props = new List<string>();
        foreach (var comp in target.GetComponents<Component>())
        {
            if (comp == null) continue;
            SerializedObject so = new SerializedObject(comp);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.Float ||
                    it.propertyType == SerializedPropertyType.Integer ||
                    it.propertyType == SerializedPropertyType.Boolean)
                    props.Add($"{comp.GetType().Name}/{it.propertyPath}");
            }
        }
        animPropCache[target] = props;
    }

    void CacheMaterialProps(Material material)
    {
        var props = new List<string>();
        var propTypes = new Dictionary<string, ShaderUtil.ShaderPropertyType>();
        Shader shader = material.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);
        
        for (int i = 0; i < propCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);
            
            if (propType == ShaderUtil.ShaderPropertyType.Float || 
                propType == ShaderUtil.ShaderPropertyType.Range ||
                propType == ShaderUtil.ShaderPropertyType.Color ||
                propType == ShaderUtil.ShaderPropertyType.Vector)
            {
                string displayName = $"material.{propName}";
                if (propType == ShaderUtil.ShaderPropertyType.Color)
                    displayName += " (Color)";
                else if (propType == ShaderUtil.ShaderPropertyType.Range)
                    displayName += " (Range)";
                
                props.Add(displayName);
                propTypes[propName] = propType;
            }
        }
        materialPropCache[material] = props;
        materialPropTypes[material] = propTypes;
    }

    List<GameObject> FindObjectsWithMaterial(GameObject root, Material material)
    {
        List<GameObject> result = new List<GameObject>();
        if (root == null || material == null) return result;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterials.Contains(material))
            {
                result.Add(renderer.gameObject);
            }
        }
        return result;
    }
}

[CustomEditor(typeof(CCKEnhancementComponent))]
public class CCKEnhancementComponentEditor : Editor
{
    private Dictionary<GameObject, List<string>> animPropCache = new Dictionary<GameObject, List<string>>();
    private Dictionary<Material, List<string>> materialPropCache = new Dictionary<Material, List<string>>();
    private Dictionary<Material, Dictionary<string, ShaderUtil.ShaderPropertyType>> materialPropTypes = new Dictionary<Material, Dictionary<string, ShaderUtil.ShaderPropertyType>>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CCKEnhancementComponent component = (CCKEnhancementComponent)target;

        component.avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", component.avatarRoot, typeof(GameObject), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1) Merge Multiple Animators", EditorStyles.boldLabel);
        DrawAnimatorMergeUI(component);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2) Generate Property Animations + Avatar Entry", EditorStyles.boldLabel);
        DrawPropertyAnimationGenerationUI(component);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3) Merge Multiple Animation Clips to One Avatar Entry", EditorStyles.boldLabel);
        DrawMultiClipMergeUI(component);

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
            EditorUtility.SetDirty(target);
    }

    void DrawAnimatorMergeUI(CCKEnhancementComponent component)
    {
        int remIdx = -1;
        for (int i = 0; i < component.animatorMergeData.animators.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            component.animatorMergeData.animators[i] = (AnimatorController)EditorGUILayout.ObjectField(component.animatorMergeData.animators[i], typeof(AnimatorController), false);
            if (GUILayout.Button("X", GUILayout.Width(20))) remIdx = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remIdx >= 0) component.animatorMergeData.animators.RemoveAt(remIdx);

        if (GUILayout.Button("Add Animator Controller"))
            component.animatorMergeData.animators.Add(null);

        if (GUILayout.Button("Merge Animators"))
        {
            if (CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(component.avatarRoot, component.animatorMergeData.animators))
                CCKEnhancementEditorHelpers.PerformAnimatorMerge(component.animatorMergeData.animators, component.avatarRoot);
        }
    }

    void DrawPropertyAnimationGenerationUI(CCKEnhancementComponent component)
    {
        int remIdx = -1;
        for (int i = 0; i < component.propertyAnimGenData.Count; i++)
        {
            var entry = component.propertyAnimGenData[i];
            EditorGUILayout.BeginVertical("box");

            entry.useMaterialProperty = EditorGUILayout.Toggle("Use Material Property", entry.useMaterialProperty);

            if (entry.useMaterialProperty)
            {
                entry.targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", entry.targetMaterial, typeof(Material), false);

                if (entry.targetMaterial != null)
                {
                    if (GUILayout.Button("Find Objects with Material"))
                    {
                        var foundObjects = FindObjectsWithMaterial(component.avatarRoot, entry.targetMaterial);
                        entry.materialTargetObjects = foundObjects.Select(go => new MaterialTargetObject { gameObject = go, enabled = true }).ToList();
                        Debug.Log($"Found {entry.materialTargetObjects.Count} objects with material '{entry.targetMaterial.name}'");
                    }

                    if (entry.materialTargetObjects.Count > 0)
                    {
                        EditorGUILayout.LabelField($"Objects with material ({entry.materialTargetObjects.Count}):", EditorStyles.boldLabel);
                        
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Select All", GUILayout.Width(100)))
                        {
                            foreach (var obj in entry.materialTargetObjects)
                                obj.enabled = true;
                        }
                        if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                        {
                            foreach (var obj in entry.materialTargetObjects)
                                obj.enabled = false;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space(5);

                        for (int j = 0; j < entry.materialTargetObjects.Count; j++)
                        {
                            var matTarget = entry.materialTargetObjects[j];
                            EditorGUILayout.BeginHorizontal();
                            matTarget.enabled = EditorGUILayout.Toggle(matTarget.enabled, GUILayout.Width(20));
                            EditorGUILayout.ObjectField(matTarget.gameObject, typeof(GameObject), true);
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.Space(5);

                    if (!materialPropCache.ContainsKey(entry.targetMaterial))
                        CacheMaterialProps(entry.targetMaterial);
                    var props = materialPropCache[entry.targetMaterial];
                    if (props.Count > 0)
                    {
                        int selIdx = string.IsNullOrEmpty(entry.serializedProperty) ? 0 : props.IndexOf(entry.serializedProperty);
                        selIdx = Mathf.Clamp(selIdx, 0, props.Count - 1);
                        selIdx = EditorGUILayout.Popup("Material Property", selIdx, props.ToArray());
                        entry.serializedProperty = props[selIdx];

                        if (!string.IsNullOrEmpty(entry.serializedProperty))
                        {
                            string propName = entry.serializedProperty.Split(new[] { ' ' }, 2)[0].Replace("material.", "");
                            if (materialPropTypes.ContainsKey(entry.targetMaterial) && 
                                materialPropTypes[entry.targetMaterial].ContainsKey(propName))
                            {
                                var propType = materialPropTypes[entry.targetMaterial][propName];
                                if (propType == ShaderUtil.ShaderPropertyType.Color)
                                    entry.advAvatarPropertyType = AdvAvatarPropertyType.Color;
                                else
                                    entry.advAvatarPropertyType = AdvAvatarPropertyType.Float;
                            }
                        }
                    }
                    else EditorGUILayout.LabelField("No animatable properties found.");
                }
                else EditorGUILayout.LabelField("Select Target Material first.");
            }
            else
            {
                entry.targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", entry.targetObject, typeof(GameObject), true);

                if (entry.targetObject != null)
                {
                    if (!animPropCache.ContainsKey(entry.targetObject))
                        CacheAnimProps(entry.targetObject);
                    var props = animPropCache[entry.targetObject];
                    if (props.Count > 0)
                    {
                        int selIdx = string.IsNullOrEmpty(entry.serializedProperty) ? 0 : props.IndexOf(entry.serializedProperty);
                        selIdx = Mathf.Clamp(selIdx, 0, props.Count - 1);
                        selIdx = EditorGUILayout.Popup("Serialized Property", selIdx, props.ToArray());
                        entry.serializedProperty = props[selIdx];
                    }
                    else EditorGUILayout.LabelField("No animatable properties found.");
                }
                else EditorGUILayout.LabelField("Select Target Object first.");
            }

            entry.advAvatarPropertyName = EditorGUILayout.TextField("Advanced Avatar Property Name", entry.advAvatarPropertyName);
            
            if (!entry.useMaterialProperty)
                entry.advAvatarPropertyType = (AdvAvatarPropertyType)EditorGUILayout.EnumPopup("Property Type", entry.advAvatarPropertyType);
            else
                EditorGUILayout.LabelField("Property Type (Auto-detected)", entry.advAvatarPropertyType.ToString());
            
            EditorGUILayout.LabelField("CVR Advanced Settings Type", EditorStyles.boldLabel);
            entry.cvrSettingsType = (CVRSettingsType)EditorGUILayout.EnumPopup("Settings Type", entry.cvrSettingsType);

            if (entry.cvrSettingsType == CVRSettingsType.Color)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Default Color", entry.colorValue0);
                    EditorGUILayout.HelpBox("Color type creates a color picker in-game. No animation clips generated.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Color settings type only works with Color property types.", MessageType.Warning);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Dropdown)
            {
                entry.dropdownOptionCount = EditorGUILayout.IntField("Number of Options", Mathf.Max(2, entry.dropdownOptionCount));
                
                while (entry.dropdownOptionNames.Count < entry.dropdownOptionCount)
                    entry.dropdownOptionNames.Add($"Option {entry.dropdownOptionNames.Count}");
                while (entry.dropdownOptionNames.Count > entry.dropdownOptionCount)
                    entry.dropdownOptionNames.RemoveAt(entry.dropdownOptionNames.Count - 1);
                
                EditorGUILayout.LabelField("Dropdown Options:", EditorStyles.boldLabel);
                for (int j = 0; j < entry.dropdownOptionNames.Count; j++)
                {
                    entry.dropdownOptionNames[j] = EditorGUILayout.TextField($"  Option {j}", entry.dropdownOptionNames[j]);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Slider)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Color Value 0 (Min)", entry.colorValue0);
                    entry.colorValue1 = EditorGUILayout.ColorField("Color Value 1 (Max)", entry.colorValue1);
                }
                else
                {
                    entry.floatValue0 = EditorGUILayout.FloatField("Float Value 0 (Min)", entry.floatValue0);
                    entry.floatValue1 = EditorGUILayout.FloatField("Float Value 1 (Max)", entry.floatValue1);
                }
            }
            else if (entry.cvrSettingsType == CVRSettingsType.Toggle)
            {
                if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Bool)
                {
                    EditorGUILayout.LabelField("Bool Value 0: False, Bool Value 1: True");
                    entry.reverseGeneration = EditorGUILayout.Toggle("Reverse Generation (Swap False/True)", entry.reverseGeneration);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                {
                    entry.floatValue0 = EditorGUILayout.FloatField("Float Value 0 (Off)", entry.floatValue0);
                    entry.floatValue1 = EditorGUILayout.FloatField("Float Value 1 (On)", entry.floatValue1);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Int)
                {
                    entry.intValue0 = EditorGUILayout.IntField("Int Value 0 (Off)", entry.intValue0);
                    entry.intValue1 = EditorGUILayout.IntField("Int Value 1 (On)", entry.intValue1);
                }
                else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                {
                    entry.colorValue0 = EditorGUILayout.ColorField("Color Value 0 (Off)", entry.colorValue0);
                    entry.colorValue1 = EditorGUILayout.ColorField("Color Value 1 (On)", entry.colorValue1);
                }
            }

            if (GUILayout.Button("Remove Entry"))
                remIdx = i;

            EditorGUILayout.EndVertical();
        }
        if (remIdx >= 0) component.propertyAnimGenData.RemoveAt(remIdx);

        if (GUILayout.Button("Add Property Animation Entry"))
            component.propertyAnimGenData.Add(new PropertyAnimGenerationData());

        if (GUILayout.Button("Generate Animations + Avatar Entries"))
        {
            if (!CCKEnhancementEditorHelpers.ValidateAvatar(component.avatarRoot)) return;
            if (component.propertyAnimGenData.Count == 0)
                EditorUtility.DisplayDialog("Error", "Add at least one property animation entry", "OK");
            else
                CCKEnhancementEditorHelpers.GeneratePropertyAnimationsWithAvatarEntries(component.avatarRoot, component.propertyAnimGenData);
        }
    }

    void DrawMultiClipMergeUI(CCKEnhancementComponent component)
    {
        int remIdx = -1;
        for (int i = 0; i < component.multiClipMergeData.Count; i++)
        {
            var entry = component.multiClipMergeData[i];
            EditorGUILayout.BeginVertical("box");

            entry.advAvatarPropertyName = EditorGUILayout.TextField("Advanced Avatar Property Name", entry.advAvatarPropertyName);
            entry.advAvatarPropertyType = (AdvAvatarPropertyType)EditorGUILayout.EnumPopup("Advanced Avatar Property Type", entry.advAvatarPropertyType);

            int remClipIdx = -1;
            EditorGUILayout.LabelField("Animation Clips");
            for (int j = 0; j < entry.animationClips.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                entry.animationClips[j] = (AnimationClip)EditorGUILayout.ObjectField(entry.animationClips[j], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    remClipIdx = j;
                EditorGUILayout.EndHorizontal();
            }
            if (remClipIdx >= 0) entry.animationClips.RemoveAt(remClipIdx);

            if (GUILayout.Button("Add Animation Clip"))
                entry.animationClips.Add(null);

            if (GUILayout.Button("Remove Entry"))
                remIdx = i;

            EditorGUILayout.EndVertical();
        }
        if (remIdx >= 0) component.multiClipMergeData.RemoveAt(remIdx);

        if (GUILayout.Button("Add Multi Clip Merge Entry"))
            component.multiClipMergeData.Add(new MultiClipMergeData());

        if (GUILayout.Button("Merge Clips to Avatar Entries"))
        {
            if (!CCKEnhancementEditorHelpers.ValidateAvatar(component.avatarRoot)) return;
            if (component.multiClipMergeData.Count == 0)
                EditorUtility.DisplayDialog("Error", "Add at least one merging entry", "OK");
            else
                CCKEnhancementEditorHelpers.MergeMultipleClipsToAvatarEntries(component.avatarRoot, component.multiClipMergeData);
        }
    }

    void CacheAnimProps(GameObject target)
    {
        var props = new List<string>();
        foreach (var comp in target.GetComponents<Component>())
        {
            if (comp == null) continue;
            SerializedObject so = new SerializedObject(comp);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.Float ||
                    it.propertyType == SerializedPropertyType.Integer ||
                    it.propertyType == SerializedPropertyType.Boolean)
                    props.Add($"{comp.GetType().Name}/{it.propertyPath}");
            }
        }
        animPropCache[target] = props;
    }

    void CacheMaterialProps(Material material)
    {
        var props = new List<string>();
        var propTypes = new Dictionary<string, ShaderUtil.ShaderPropertyType>();
        Shader shader = material.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);
        
        for (int i = 0; i < propCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);
            
            if (propType == ShaderUtil.ShaderPropertyType.Float || 
                propType == ShaderUtil.ShaderPropertyType.Range ||
                propType == ShaderUtil.ShaderPropertyType.Color ||
                propType == ShaderUtil.ShaderPropertyType.Vector)
            {
                string displayName = $"material.{propName}";
                if (propType == ShaderUtil.ShaderPropertyType.Color)
                    displayName += " (Color)";
                else if (propType == ShaderUtil.ShaderPropertyType.Range)
                    displayName += " (Range)";
                
                props.Add(displayName);
                propTypes[propName] = propType;
            }
        }
        materialPropCache[material] = props;
        materialPropTypes[material] = propTypes;
    }

    List<GameObject> FindObjectsWithMaterial(GameObject root, Material material)
    {
        List<GameObject> result = new List<GameObject>();
        if (root == null || material == null) return result;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterials.Contains(material))
            {
                result.Add(renderer.gameObject);
            }
        }
        return result;
    }
}

public static class CCKEnhancementEditorHelpers
{
    public static bool ValidateAvatar(GameObject avatarRoot)
    {
        if (avatarRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign Avatar Root", "OK");
            return false;
        }
        return true;
    }

    public static bool ValidateAvatarAndAnimators(GameObject avatarRoot, List<AnimatorController> list)
    {
        if (!ValidateAvatar(avatarRoot))
            return false;
        if (list == null || list.Count < 1)
        {
            EditorUtility.DisplayDialog("Error", "Add at least 1 animator controller to merge", "OK");
            return false;
        }
        return true;
    }

    public static void PerformAnimatorMerge(List<AnimatorController> anims, GameObject avatar)
    {
        if (anims == null || anims.Count < 1) 
        {
            EditorUtility.DisplayDialog("Error", "Add at least 1 animator controller to merge", "OK");
            return;
        }

        CVRAvatar cvrAvatar = avatar.GetComponent<CVRAvatar>();
        if (cvrAvatar == null)
        {
            EditorUtility.DisplayDialog("Error", "No CVRAvatar component found on avatar root!", "OK");
            return;
        }

        if (cvrAvatar.avatarSettings == null || cvrAvatar.avatarSettings.baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "No base controller found in avatar settings. Please set up advanced avatar settings first.", "OK");
            return;
        }

        AnimatorController baseController = cvrAvatar.avatarSettings.baseController as AnimatorController;
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "Base controller is not an AnimatorController", "OK");
            return;
        }

        string folder = "Assets/AdvancedSettings.Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "AdvancedSettings.Generated");

        string backupFolder = $"{folder}/Backups";
        if (!AssetDatabase.IsValidFolder(backupFolder))
            AssetDatabase.CreateFolder(folder, "Backups");

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseControllerPath = AssetDatabase.GetAssetPath(baseController);
        string backupPath = $"{backupFolder}/{baseController.name}_Backup_{timestamp}.controller";
        
        if (!AssetDatabase.CopyAsset(baseControllerPath, backupPath))
        {
            EditorUtility.DisplayDialog("Error", "Failed to create backup of base controller", "OK");
            return;
        }

        Debug.Log($"Created backup of base controller at: {backupPath}");

        foreach (var anim in anims)
        {
            if (anim == null) continue;

            foreach (var param in anim.parameters)
            {
                if (!baseController.parameters.Any(p => p.name == param.name && p.type == param.type))
                {
                    baseController.AddParameter(param);
                }
            }

            foreach (var layer in anim.layers)
            {
                bool layerExists = false;
                foreach (var existingLayer in baseController.layers)
                {
                    if (existingLayer.name == layer.name)
                    {
                        layerExists = true;
                        break;
                    }
                }

                string layerName = layer.name;
                int suffix = 1;
                while (layerExists)
                {
                    layerName = $"{layer.name}_{suffix}";
                    layerExists = false;
                    foreach (var existingLayer in baseController.layers)
                    {
                        if (existingLayer.name == layerName)
                        {
                            layerExists = true;
                            break;
                        }
                    }
                    suffix++;
                }

                var newLayer = new AnimatorControllerLayer()
                {
                    name = layerName,
                    defaultWeight = layer.defaultWeight,
                    stateMachine = UnityEngine.Object.Instantiate(layer.stateMachine)
                };
                baseController.AddLayer(newLayer);
            }
        }

        EditorUtility.SetDirty(baseController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Animator Merge Complete", 
            $"Successfully merged {anims.Count} animator(s) into base controller.\n\n" +
            $"Backup saved at:\n{backupPath}", 
            "OK");
    }

    public static void GeneratePropertyAnimationsWithAvatarEntries(GameObject avatarRoot, List<PropertyAnimGenerationData> entries)
    {
        if (avatarRoot == null || entries == null) return;

        CVRAvatar cvrAvatar = avatarRoot.GetComponent<CVRAvatar>();
        if (cvrAvatar == null)
        {
            Debug.LogError("No CVRAvatar component found on avatar root!");
            return;
        }

        if (cvrAvatar.avatarSettings == null || !cvrAvatar.avatarSettings.initialized)
        {
            cvrAvatar.avatarSettings = new CVRAdvancedAvatarSettings
            {
                settings = new List<CVRAdvancedSettingsEntry>(),
                initialized = true
            };
        }

        string folder = "Assets/AdvancedSettings.Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "AdvancedSettings.Generated");

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.serializedProperty) || string.IsNullOrEmpty(entry.advAvatarPropertyName))
                continue;

            List<AnimationClip> generatedClips = new List<AnimationClip>();

            if (entry.cvrSettingsType == CVRSettingsType.Dropdown)
            {
                for (int optIdx = 0; optIdx < entry.dropdownOptionCount; optIdx++)
                {
                    AnimationClip clip = new AnimationClip();
                    
                    if (entry.useMaterialProperty)
                    {
                        var enabledObjects = entry.materialTargetObjects.Where(o => o.enabled && o.gameObject != null).ToList();
                        string propName = entry.serializedProperty.Split(new[] { ' ' }, 2)[0].Replace("material.", "");
                        
                        foreach (var matTarget in enabledObjects)
                        {
                            string relativePath = AnimationUtility.CalculateTransformPath(matTarget.gameObject.transform, avatarRoot.transform);
                            string materialPropPath = $"material.{propName}";
                            
                            if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                            {
                                float value = Mathf.Lerp(entry.floatValue0, entry.floatValue1, (float)optIdx / (entry.dropdownOptionCount - 1));
                                clip.SetCurve(relativePath, typeof(Renderer), materialPropPath, AnimationCurve.Constant(0, 1, value));
                            }
                            else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                            {
                                Color value = Color.Lerp(entry.colorValue0, entry.colorValue1, (float)optIdx / (entry.dropdownOptionCount - 1));
                                clip.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.r", AnimationCurve.Constant(0, 1, value.r));
                                clip.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.g", AnimationCurve.Constant(0, 1, value.g));
                                clip.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.b", AnimationCurve.Constant(0, 1, value.b));
                                clip.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.a", AnimationCurve.Constant(0, 1, value.a));
                            }
                        }
                    }
                    else
                    {
                        string relativePath = AnimationUtility.CalculateTransformPath(entry.targetObject.transform, avatarRoot.transform);
                        string[] parts = entry.serializedProperty.Split(new char[] { '/' }, 2);
                        string compName = parts[0];
                        string propName = parts.Length > 1 ? parts[1] : "";
                        Component comp = entry.targetObject.GetComponent(compName);
                        Type compType = comp.GetType();
                        
                        if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                        {
                            float value = Mathf.Lerp(entry.floatValue0, entry.floatValue1, (float)optIdx / (entry.dropdownOptionCount - 1));
                            clip.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, value));
                        }
                        else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Int)
                        {
                            int value = Mathf.RoundToInt(Mathf.Lerp(entry.intValue0, entry.intValue1, (float)optIdx / (entry.dropdownOptionCount - 1)));
                            clip.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, value));
                        }
                    }
                    
                    string clipName = $"{avatarRoot.name}_{entry.advAvatarPropertyName}_{entry.dropdownOptionNames[optIdx]}.anim";
                    string clipPath = $"{folder}/{clipName}";
                    AssetDatabase.CreateAsset(clip, clipPath);
                    generatedClips.Add(clip);
                }
            }
            else if (entry.cvrSettingsType != CVRSettingsType.Color)
            {
                AnimationClip clip0 = new AnimationClip();
                AnimationClip clip1 = new AnimationClip();

                if (entry.useMaterialProperty)
                {
                    var enabledObjects = entry.materialTargetObjects.Where(o => o.enabled && o.gameObject != null).ToList();
                    if (enabledObjects.Count == 0)
                    {
                        Debug.LogWarning($"No enabled objects selected for material property '{entry.advAvatarPropertyName}'");
                        continue;
                    }

                    string propName = entry.serializedProperty.Split(new[] { ' ' }, 2)[0].Replace("material.", "");

                    foreach (var matTarget in enabledObjects)
                    {
                        string relativePath = AnimationUtility.CalculateTransformPath(matTarget.gameObject.transform, avatarRoot.transform);
                        string materialPropPath = $"material.{propName}";

                        if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                        {
                            clip0.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.r", AnimationCurve.Constant(0, 1, entry.colorValue0.r));
                            clip0.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.g", AnimationCurve.Constant(0, 1, entry.colorValue0.g));
                            clip0.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.b", AnimationCurve.Constant(0, 1, entry.colorValue0.b));
                            clip0.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.a", AnimationCurve.Constant(0, 1, entry.colorValue0.a));

                            clip1.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.r", AnimationCurve.Constant(0, 1, entry.colorValue1.r));
                            clip1.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.g", AnimationCurve.Constant(0, 1, entry.colorValue1.g));
                            clip1.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.b", AnimationCurve.Constant(0, 1, entry.colorValue1.b));
                            clip1.SetCurve(relativePath, typeof(Renderer), $"{materialPropPath}.a", AnimationCurve.Constant(0, 1, entry.colorValue1.a));
                        }
                        else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                        {
                            clip0.SetCurve(relativePath, typeof(Renderer), materialPropPath, AnimationCurve.Constant(0, 1, entry.floatValue0));
                            clip1.SetCurve(relativePath, typeof(Renderer), materialPropPath, AnimationCurve.Constant(0, 1, entry.floatValue1));
                        }
                    }
                }
                else
                {
                    if (entry.targetObject == null)
                    {
                        Debug.LogWarning($"Target object not set for '{entry.advAvatarPropertyName}'");
                        continue;
                    }

                    string relativePath = AnimationUtility.CalculateTransformPath(entry.targetObject.transform, avatarRoot.transform);
                    string[] parts = entry.serializedProperty.Split(new char[] { '/' }, 2);
                    string compName = parts[0];
                    string propName = parts.Length > 1 ? parts[1] : "";

                    Component comp = entry.targetObject.GetComponent(compName);
                    if (comp == null)
                    {
                        Debug.LogWarning($"Component '{compName}' missing from {entry.targetObject.name}");
                        continue;
                    }
                    Type compType = comp.GetType();

                    if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Bool)
                    {
                        if (!entry.reverseGeneration)
                        {
                            clip0.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, 0));
                            clip1.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, 1));
                        }
                        else
                        {
                            clip0.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, 1));
                            clip1.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, 0));
                        }
                    }
                    else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
                    {
                        clip0.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, entry.floatValue0));
                        clip1.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, entry.floatValue1));
                    }
                    else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Int)
                    {
                        clip0.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, entry.intValue0));
                        clip1.SetCurve(relativePath, compType, propName, AnimationCurve.Constant(0, 1, entry.intValue1));
                    }
                    else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Color)
                    {
                        clip0.SetCurve(relativePath, compType, $"{propName}.r", AnimationCurve.Constant(0, 1, entry.colorValue0.r));
                        clip0.SetCurve(relativePath, compType, $"{propName}.g", AnimationCurve.Constant(0, 1, entry.colorValue0.g));
                        clip0.SetCurve(relativePath, compType, $"{propName}.b", AnimationCurve.Constant(0, 1, entry.colorValue0.b));
                        clip0.SetCurve(relativePath, compType, $"{propName}.a", AnimationCurve.Constant(0, 1, entry.colorValue0.a));

                        clip1.SetCurve(relativePath, compType, $"{propName}.r", AnimationCurve.Constant(0, 1, entry.colorValue1.r));
                        clip1.SetCurve(relativePath, compType, $"{propName}.g", AnimationCurve.Constant(0, 1, entry.colorValue1.g));
                        clip1.SetCurve(relativePath, compType, $"{propName}.b", AnimationCurve.Constant(0, 1, entry.colorValue1.b));
                        clip1.SetCurve(relativePath, compType, $"{propName}.a", AnimationCurve.Constant(0, 1, entry.colorValue1.a));
                    }
                }

                string clip0Name = $"{avatarRoot.name}_{entry.advAvatarPropertyName}_Off.anim";
                string clip1Name = $"{avatarRoot.name}_{entry.advAvatarPropertyName}_On.anim";

                string clip0Path = $"{folder}/{clip0Name}";
                string clip1Path = $"{folder}/{clip1Name}";

                AssetDatabase.CreateAsset(clip0, clip0Path);
                AssetDatabase.CreateAsset(clip1, clip1Path);
                
                generatedClips.Add(clip0);
                generatedClips.Add(clip1);
            }

            AssetDatabase.SaveAssets();

            CVRAdvancedSettingsEntry newEntry = new CVRAdvancedSettingsEntry
            {
                name = entry.advAvatarPropertyName,
                machineName = entry.advAvatarPropertyName
            };

            switch (entry.cvrSettingsType)
            {
                case CVRSettingsType.Toggle:
                    newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Toggle;
                    newEntry.toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                    {
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Bool,
                        defaultValue = false,
                        useAnimationClip = true,
                        offAnimationClip = generatedClips[0],
                        animationClip = generatedClips[1]
                    };
                    break;

                case CVRSettingsType.Slider:
                    newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Slider;
                    newEntry.sliderSettings = new CVRAdvancesAvatarSettingSlider
                    {
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Float,
                        defaultValue = entry.floatValue0,
                        useAnimationClip = true,
                        minAnimationClip = generatedClips[0],
                        maxAnimationClip = generatedClips[1]
                    };
                    break;

                case CVRSettingsType.Dropdown:
                    newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Dropdown;
                    var dropdownOptions = new List<CVRAdvancedSettingsDropDownEntry>();
                    for (int i = 0; i < generatedClips.Count; i++)
                    {
                        dropdownOptions.Add(new CVRAdvancedSettingsDropDownEntry
                        {
                            name = entry.dropdownOptionNames[i],
                            useAnimationClip = true,
                            animationClip = generatedClips[i]
                        });
                    }
                    newEntry.dropDownSettings = new CVRAdvancesAvatarSettingGameObjectDropdown
                    {
                        usedType = CVRAdvancesAvatarSettingBase.ParameterType.Int,
                        defaultValue = 0,
                        options = dropdownOptions
                    };
                    break;

                case CVRSettingsType.Color:
                    newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Color;
                    newEntry.setting = new CVRAdvancedAvatarSettingMaterialColor
                    {
                        defaultValue = entry.colorValue0
                    };
                    break;
            }

            cvrAvatar.avatarSettings.settings.Add(newEntry);
            Debug.Log($"Added advanced avatar entry '{entry.advAvatarPropertyName}' of type '{entry.cvrSettingsType}'");
        }

        EditorUtility.SetDirty(cvrAvatar);
        EditorUtility.DisplayDialog("Property Animations", $"Generated {entries.Count} property animations and added them to advanced avatar settings.", "OK");
        AssetDatabase.Refresh();
    }

    public static void MergeMultipleClipsToAvatarEntries(GameObject avatarRoot, List<MultiClipMergeData> entries)
    {
        if (avatarRoot == null || entries == null) return;

        CVRAvatar cvrAvatar = avatarRoot.GetComponent<CVRAvatar>();
        if (cvrAvatar == null)
        {
            Debug.LogError("No CVRAvatar component found on avatar root!");
            return;
        }

        if (cvrAvatar.avatarSettings == null || !cvrAvatar.avatarSettings.initialized)
        {
            cvrAvatar.avatarSettings = new CVRAdvancedAvatarSettings
            {
                settings = new List<CVRAdvancedSettingsEntry>(),
                initialized = true
            };
        }

        string folder = "Assets/AdvancedSettings.Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "AdvancedSettings.Generated");

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.advAvatarPropertyName) || entry.animationClips == null || entry.animationClips.Count == 0)
                continue;

            AnimationClip clip;
            if (entry.animationClips.Count == 1)
                clip = entry.animationClips[0];
            else
                clip = BlendClips(entry.animationClips, entry.advAvatarPropertyName, folder);

            if (clip == null) continue;

            string savePath = $"{folder}/{avatarRoot.name}_{entry.advAvatarPropertyName}.anim";

            if (entry.animationClips.Count > 1)
            {
                AssetDatabase.CreateAsset(clip, savePath);
                AssetDatabase.SaveAssets();
            }

            CVRAdvancedSettingsEntry newEntry = new CVRAdvancedSettingsEntry
            {
                name = entry.advAvatarPropertyName,
                machineName = entry.advAvatarPropertyName
            };

            if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Bool)
            {
                newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Toggle;
                newEntry.toggleSettings = new CVRAdvancesAvatarSettingGameObjectToggle
                {
                    usedType = CVRAdvancesAvatarSettingBase.ParameterType.Bool,
                    defaultValue = false,
                    useAnimationClip = true,
                    animationClip = clip
                };
            }
            else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Float)
            {
                newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Slider;
                newEntry.sliderSettings = new CVRAdvancesAvatarSettingSlider
                {
                    usedType = CVRAdvancesAvatarSettingBase.ParameterType.Float,
                    defaultValue = 0f,
                    useAnimationClip = true,
                    maxAnimationClip = clip
                };
            }
            else if (entry.advAvatarPropertyType == AdvAvatarPropertyType.Int)
            {
                newEntry.type = CVRAdvancedSettingsEntry.SettingsType.Dropdown;
                var dropdownOptions = new List<CVRAdvancedSettingsDropDownEntry>();
                for (int i = 0; i < entry.animationClips.Count; i++)
                {
                    dropdownOptions.Add(new CVRAdvancedSettingsDropDownEntry
                    {
                        name = $"Option {i}",
                        useAnimationClip = true,
                        animationClip = entry.animationClips[i]
                    });
                }
                newEntry.dropDownSettings = new CVRAdvancesAvatarSettingGameObjectDropdown
                {
                    usedType = CVRAdvancesAvatarSettingBase.ParameterType.Int,
                    defaultValue = 0,
                    options = dropdownOptions
                };
            }

            cvrAvatar.avatarSettings.settings.Add(newEntry);
            Debug.Log($"Added merged animation clip for '{entry.advAvatarPropertyName}' to advanced avatar settings.");
        }

        EditorUtility.SetDirty(cvrAvatar);
        EditorUtility.DisplayDialog("Multi Clip Merge", $"Generated {entries.Count} merged animations and added them to advanced avatar settings.", "OK");
        AssetDatabase.Refresh();
    }

    private static AnimationClip BlendClips(List<AnimationClip> clips, string name, string folder)
    {
        AnimationClip newClip = new AnimationClip() { name = name };

        var bindings = clips.SelectMany(c => AnimationUtility.GetCurveBindings(c)).Distinct().ToList();

        foreach (var binding in bindings)
        {
            List<AnimationCurve> curves = new List<AnimationCurve>();
            foreach (var clip in clips)
            {
                var c = AnimationUtility.GetEditorCurve(clip, binding);
                if (c != null)
                    curves.Add(c);
            }
            if (curves.Count == 0) continue;

            AnimationCurve blended = new AnimationCurve();

            int keyCount = curves[0].length;
            for (int k = 0; k < keyCount; k++)
            {
                float time = curves[0].keys[k].time;
                float val = curves.Average(c => c.Evaluate(time));
                blended.AddKey(new Keyframe(time, val));
            }
            newClip.SetCurve(binding.path, binding.type, binding.propertyName, blended);
        }
        return newClip;
    }
}
#endif
