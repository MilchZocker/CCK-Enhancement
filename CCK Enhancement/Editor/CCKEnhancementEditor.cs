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
    private bool showConflictsFoldout = true;

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
        if (remIdx >= 0)
        {
            animatorMergeData.animators.RemoveAt(remIdx);
            animatorMergeData.conflictsScanned = false; // invalidate scan
        }

        if (GUILayout.Button("Add Animator Controller"))
        {
            animatorMergeData.animators.Add(null);
            animatorMergeData.conflictsScanned = false;
        }

        using (new EditorGUI.DisabledScope(!CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(avatarRoot, animatorMergeData.animators)))
        {
            if (GUILayout.Button("Scan Conflicts"))
            {
                CCKEnhancementEditorHelpers.ScanAnimatorConflicts(animatorMergeData, avatarRoot);
            }
        }

        DrawConflictResolver(animatorMergeData);

        using (new EditorGUI.DisabledScope(!animatorMergeData.conflictsScanned))
        {
            if (GUILayout.Button("Merge Animators"))
            {
                if (CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(avatarRoot, animatorMergeData.animators))
                    CCKEnhancementEditorHelpers.PerformAnimatorMerge(animatorMergeData, avatarRoot);
            }
        }
    }

    void DrawConflictResolver(AnimatorMergeData data)
    {
        if (!data.conflictsScanned) return;

        showConflictsFoldout = EditorGUILayout.Foldout(showConflictsFoldout, "Animator Merge Conflicts (Resolve Before Merge)", true);
        if (!showConflictsFoldout) return;

        EditorGUILayout.BeginVertical("box");

        // Parameters
        EditorGUILayout.LabelField("Parameter Conflicts (Used Parameters Only)", EditorStyles.boldLabel);
        if (data.parameterConflicts.Count == 0)
        {
            EditorGUILayout.LabelField("No parameter conflicts found.");
        }
        else
        {
            foreach (var c in data.parameterConflicts)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Incoming: {c.incomingParameterName} ({c.incomingType})");
                EditorGUILayout.LabelField($"Base: {c.incomingParameterName} ({c.baseType})");

                c.resolution = (ParameterConflictResolution)EditorGUILayout.EnumPopup("Resolution", c.resolution);
                if (c.resolution == ParameterConflictResolution.Rename)
                {
                    c.resolvedParameterName = EditorGUILayout.TextField("Rename To", c.resolvedParameterName);
                }

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(6);

        // Layers
        EditorGUILayout.LabelField("Layer Conflicts", EditorStyles.boldLabel);
        if (data.layerConflicts.Count == 0)
        {
            EditorGUILayout.LabelField("No layer conflicts found.");
        }
        else
        {
            foreach (var c in data.layerConflicts)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Incoming: {c.incomingLayerName}");
                EditorGUILayout.LabelField($"Base: {c.baseLayerName}");

                c.resolution = (LayerConflictResolution)EditorGUILayout.EnumPopup("Resolution", c.resolution);
                if (c.resolution == LayerConflictResolution.Rename)
                {
                    c.resolvedLayerName = EditorGUILayout.TextField("Rename To", c.resolvedLayerName);
                }

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
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
    private bool showConflictsFoldout = true;

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
        var data = component.animatorMergeData;

        int remIdx = -1;
        for (int i = 0; i < data.animators.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            data.animators[i] = (AnimatorController)EditorGUILayout.ObjectField(data.animators[i], typeof(AnimatorController), false);
            if (GUILayout.Button("X", GUILayout.Width(20))) remIdx = i;
            EditorGUILayout.EndHorizontal();
        }

        if (remIdx >= 0)
        {
            data.animators.RemoveAt(remIdx);
            data.conflictsScanned = false;
        }

        if (GUILayout.Button("Add Animator Controller"))
        {
            data.animators.Add(null);
            data.conflictsScanned = false;
        }

        using (new EditorGUI.DisabledScope(!CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(component.avatarRoot, data.animators)))
        {
            if (GUILayout.Button("Scan Conflicts"))
            {
                CCKEnhancementEditorHelpers.ScanAnimatorConflicts(data, component.avatarRoot);
            }
        }

        DrawConflictResolver(data);

        using (new EditorGUI.DisabledScope(!data.conflictsScanned))
        {
            if (GUILayout.Button("Merge Animators"))
            {
                if (CCKEnhancementEditorHelpers.ValidateAvatarAndAnimators(component.avatarRoot, data.animators))
                    CCKEnhancementEditorHelpers.PerformAnimatorMerge(data, component.avatarRoot);
            }
        }
    }

    void DrawConflictResolver(AnimatorMergeData data)
    {
        if (!data.conflictsScanned) return;

        showConflictsFoldout = EditorGUILayout.Foldout(showConflictsFoldout, "Animator Merge Conflicts (Resolve Before Merge)", true);
        if (!showConflictsFoldout) return;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Parameter Conflicts (Used Parameters Only)", EditorStyles.boldLabel);
        if (data.parameterConflicts.Count == 0)
        {
            EditorGUILayout.LabelField("No parameter conflicts found.");
        }
        else
        {
            foreach (var c in data.parameterConflicts)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Incoming: {c.incomingParameterName} ({c.incomingType})");
                EditorGUILayout.LabelField($"Base: {c.incomingParameterName} ({c.baseType})");
                c.resolution = (ParameterConflictResolution)EditorGUILayout.EnumPopup("Resolution", c.resolution);
                if (c.resolution == ParameterConflictResolution.Rename)
                {
                    c.resolvedParameterName = EditorGUILayout.TextField("Rename To", c.resolvedParameterName);
                }
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Layer Conflicts", EditorStyles.boldLabel);
        if (data.layerConflicts.Count == 0)
        {
            EditorGUILayout.LabelField("No layer conflicts found.");
        }
        else
        {
            foreach (var c in data.layerConflicts)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Incoming: {c.incomingLayerName}");
                EditorGUILayout.LabelField($"Base: {c.baseLayerName}");
                c.resolution = (LayerConflictResolution)EditorGUILayout.EnumPopup("Resolution", c.resolution);
                if (c.resolution == LayerConflictResolution.Rename)
                {
                    c.resolvedLayerName = EditorGUILayout.TextField("Rename To", c.resolvedLayerName);
                }
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
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

    // =====================================================
    // PARAMETER USAGE SCAN (USED ONLY MERGE)
    // =====================================================

    private static HashSet<string> GetUsedParameters(AnimatorController controller)
    {
        var used = new HashSet<string>();
        if (controller == null) return used;

        foreach (var layer in controller.layers)
        {
            if (layer?.stateMachine == null) continue;
            CollectParametersFromStateMachine(layer.stateMachine, used);
        }

        return used;
    }

    private static void CollectParametersFromStateMachine(AnimatorStateMachine sm, HashSet<string> used)
    {
        if (sm == null) return;

        foreach (var t in sm.anyStateTransitions)
            CollectFromConditions(t.conditions, used);

        foreach (var t in sm.entryTransitions)
            CollectFromConditions(t.conditions, used);

        foreach (var child in sm.states)
        {
            var st = child.state;
            if (st == null) continue;

            CollectFromMotion(st.motion, used);

            foreach (var t in st.transitions)
                CollectFromConditions(t.conditions, used);
        }

        foreach (var childSm in sm.stateMachines)
            CollectParametersFromStateMachine(childSm.stateMachine, used);
    }

    private static void CollectFromConditions(AnimatorCondition[] conditions, HashSet<string> used)
    {
        if (conditions == null) return;
        foreach (var c in conditions)
        {
            if (!string.IsNullOrEmpty(c.parameter))
                used.Add(c.parameter);
        }
    }

    private static void CollectFromMotion(Motion motion, HashSet<string> used)
    {
        if (motion == null) return;
        if (motion is BlendTree bt)
            CollectFromBlendTree(bt, used);
    }

    private static void CollectFromBlendTree(BlendTree bt, HashSet<string> used)
    {
        if (bt == null) return;

        if (!string.IsNullOrEmpty(bt.blendParameter))
            used.Add(bt.blendParameter);

        if (!string.IsNullOrEmpty(bt.blendParameterY))
            used.Add(bt.blendParameterY);

        foreach (var child in bt.children)
        {
            if (!string.IsNullOrEmpty(child.directBlendParameter))
                used.Add(child.directBlendParameter);

            if (child.motion is BlendTree childBt)
                CollectFromBlendTree(childBt, used);
        }
    }

    // =====================================================
    //  PRE-SCAN CONFLICTS (VISIBLE RESOLVER)
    // =====================================================

    public static void ScanAnimatorConflicts(AnimatorMergeData data, GameObject avatar)
    {
        data.layerConflicts.Clear();
        data.parameterConflicts.Clear();
        data.conflictsScanned = false;

        CVRAvatar cvrAvatar = avatar.GetComponent<CVRAvatar>();
        if (cvrAvatar == null || cvrAvatar.avatarSettings == null || cvrAvatar.avatarSettings.baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "No base controller found in avatar settings.", "OK");
            return;
        }

        AnimatorController baseController = cvrAvatar.avatarSettings.baseController as AnimatorController;
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "Base controller is not an AnimatorController.", "OK");
            return;
        }

        var baseLayerNames = new HashSet<string>(baseController.layers.Select(l => l.name));
        var baseParams = baseController.parameters.ToDictionary(p => p.name, p => p.type);

        foreach (var anim in data.animators)
        {
            if (anim == null) continue;
            string srcPath = AssetDatabase.GetAssetPath(anim);

            var usedParams = GetUsedParameters(anim);

            // Parameters (USED only)
            foreach (var p in anim.parameters)
            {
                if (!usedParams.Contains(p.name))
                    continue;

                if (baseParams.TryGetValue(p.name, out var baseType))
                {
                    if (!data.parameterConflicts.Any(x => x.sourceControllerPath == srcPath && x.incomingParameterName == p.name))
                    {
                        var c = new ParameterConflict
                        {
                            sourceControllerPath = srcPath,
                            incomingParameterName = p.name,
                            incomingType = p.type,
                            baseType = baseType
                        };

                        if (baseType == p.type)
                        {
                            c.resolution = ParameterConflictResolution.UseSame;
                            c.resolvedParameterName = p.name;
                        }
                        else
                        {
                            c.resolution = ParameterConflictResolution.Rename;
                            c.resolvedParameterName = MakeUniqueParameterName(baseController, p.name);
                        }

                        data.parameterConflicts.Add(c);
                    }
                }
            }

            // Layers
            foreach (var l in anim.layers)
            {
                if (baseLayerNames.Contains(l.name))
                {
                    if (!data.layerConflicts.Any(x => x.sourceControllerPath == srcPath && x.incomingLayerName == l.name))
                    {
                        var c = new LayerConflict
                        {
                            sourceControllerPath = srcPath,
                            incomingLayerName = l.name,
                            baseLayerName = l.name,
                            resolution = LayerConflictResolution.Rename,
                            resolvedLayerName = MakeUniqueLayerName(baseController, l.name)
                        };
                        data.layerConflicts.Add(c);
                    }
                }
            }
        }

        data.conflictsScanned = true;

        EditorUtility.DisplayDialog(
            "Conflict Scan Complete",
            $"Found:\n- {data.parameterConflicts.Count} USED parameter conflict(s)\n- {data.layerConflicts.Count} layer conflict(s)\n\nResolve them below before merging.",
            "OK"
        );
    }

    // =====================================================
    //  MERGE USING SAVED RESOLUTIONS
    // =====================================================

    public static void PerformAnimatorMerge(AnimatorMergeData data, GameObject avatar)
    {
        if (data == null || data.animators == null || data.animators.Count < 1)
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

        // Ensure advanced avatar settings exist/initialized
        if (cvrAvatar.avatarSettings == null || !cvrAvatar.avatarSettings.initialized)
        {
            cvrAvatar.avatarSettings = new CVRAdvancedAvatarSettings
            {
                settings = new List<CVRAdvancedSettingsEntry>(),
                initialized = true
            };
        }

        if (cvrAvatar.avatarSettings.baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "No base controller found in avatar settings.", "OK");
            return;
        }

        AnimatorController baseController = cvrAvatar.avatarSettings.baseController as AnimatorController;
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "Base controller is not an AnimatorController", "OK");
            return;
        }

        // Create NEW merged controller asset from base
        string folder = "Assets/AdvancedSettings.Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "AdvancedSettings.Generated");

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string basePath = AssetDatabase.GetAssetPath(baseController);
        string mergedPath = $"{folder}/{baseController.name}_Merged_{timestamp}.controller";

        if (!AssetDatabase.CopyAsset(basePath, mergedPath))
        {
            EditorUtility.DisplayDialog("Error", "Failed to create merged controller asset", "OK");
            return;
        }

        AssetDatabase.ImportAsset(mergedPath);
        AnimatorController mergedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(mergedPath);
        if (mergedController == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to load merged controller asset", "OK");
            return;
        }

        foreach (var anim in data.animators)
        {
            if (anim == null) continue;
            string srcPath = AssetDatabase.GetAssetPath(anim);

            var usedParams = GetUsedParameters(anim);

            Dictionary<string, string> parameterRenameMap = new Dictionary<string, string>();

            // PARAMETERS (USED ONLY)
            foreach (var p in anim.parameters)
            {
                if (!usedParams.Contains(p.name))
                    continue;

                var conflict = data.parameterConflicts.FirstOrDefault(c =>
                    c.sourceControllerPath == srcPath && c.incomingParameterName == p.name);

                if (conflict != null)
                {
                    switch (conflict.resolution)
                    {
                        case ParameterConflictResolution.UseSame:
                        case ParameterConflictResolution.Discard:
                            continue;

                        case ParameterConflictResolution.Rename:
                            string finalName = EnsureUniqueParameterName(mergedController, conflict.resolvedParameterName);
                            if (!mergedController.parameters.Any(x => x.name == finalName))
                            {
                                mergedController.AddParameter(new AnimatorControllerParameter
                                {
                                    name = finalName,
                                    type = p.type,
                                    defaultBool = p.defaultBool,
                                    defaultFloat = p.defaultFloat,
                                    defaultInt = p.defaultInt
                                });
                            }
                            parameterRenameMap[p.name] = finalName;
                            continue;
                    }
                }

                if (!mergedController.parameters.Any(x => x.name == p.name))
                    mergedController.AddParameter(p);
            }

            // LAYERS
            foreach (var l in anim.layers)
            {
                var conflict = data.layerConflicts.FirstOrDefault(c =>
                    c.sourceControllerPath == srcPath && c.incomingLayerName == l.name);

                bool overrideLayer = false;
                string finalLayerName = l.name;

                if (conflict != null)
                {
                    switch (conflict.resolution)
                    {
                        case LayerConflictResolution.Discard:
                            continue;

                        case LayerConflictResolution.Override:
                            overrideLayer = true;
                            finalLayerName = l.name;
                            break;

                        case LayerConflictResolution.Rename:
                            finalLayerName = EnsureUniqueLayerName(mergedController, conflict.resolvedLayerName);
                            break;
                    }
                }
                else
                {
                    if (mergedController.layers.Any(x => x.name == finalLayerName))
                        finalLayerName = EnsureUniqueLayerName(mergedController, finalLayerName);
                }

                int existingIndex = FindLayerIndex(mergedController, l.name);
                if (overrideLayer && existingIndex >= 0)
                    mergedController.RemoveLayer(existingIndex);

                AnimatorStateMachine clonedSM = CloneStateMachine(l.stateMachine, l.stateMachine.name);
                AddStateMachineSubAssetsToController(mergedController, clonedSM);

                if (parameterRenameMap.Count > 0)
                    RemapParametersInStateMachine(clonedSM, parameterRenameMap);

                mergedController.AddLayer(new AnimatorControllerLayer
                {
                    name = finalLayerName,
                    defaultWeight = l.defaultWeight,
                    stateMachine = clonedSM,
                    blendingMode = l.blendingMode,
                    iKPass = l.iKPass,
                    avatarMask = l.avatarMask,
                    syncedLayerAffectsTiming = l.syncedLayerAffectsTiming,
                    syncedLayerIndex = l.syncedLayerIndex
                });
            }
        }

        // Assign merged controller as Advanced Avatar Base Controller
        Undo.RecordObject(cvrAvatar, "Assign merged base controller");
        cvrAvatar.avatarSettings.baseController = mergedController;

        EditorUtility.SetDirty(mergedController);
        EditorUtility.SetDirty(cvrAvatar);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Animator Merge Complete",
            $"Merged {data.animators.Count} animator(s) into a NEW controller.\n\nNew controller:\n{mergedPath}\n\nAssigned as Advanced Avatar Base Controller.",
            "OK"
        );
    }

    // Backwards-compatible overload
    public static void PerformAnimatorMerge(List<AnimatorController> anims, GameObject avatar)
    {
        var tmp = new AnimatorMergeData { animators = anims };
        ScanAnimatorConflicts(tmp, avatar);
        PerformAnimatorMerge(tmp, avatar);
    }

    private static int FindLayerIndex(AnimatorController controller, string layerName)
    {
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == layerName)
                return i;
        return -1;
    }

    private static string MakeUniqueLayerName(AnimatorController controller, string baseName)
    {
        string name = baseName;
        int i = 1;
        while (controller.layers.Any(l => l.name == name))
            name = $"{baseName}_{i++}";
        return name;
    }

    private static string MakeUniqueParameterName(AnimatorController controller, string baseName)
    {
        string name = baseName;
        int i = 1;
        while (controller.parameters.Any(p => p.name == name))
            name = $"{baseName}_{i++}";
        return name;
    }

    private static string EnsureUniqueLayerName(AnimatorController controller, string desired)
    {
        if (!controller.layers.Any(l => l.name == desired)) return desired;
        return MakeUniqueLayerName(controller, desired);
    }

    private static string EnsureUniqueParameterName(AnimatorController controller, string desired)
    {
        if (!controller.parameters.Any(p => p.name == desired)) return desired;
        return MakeUniqueParameterName(controller, desired);
    }

    // ---------- SAFE CLONE (no Instantiate, fixed ambiguous CS0121 via casts) ----------
    private static AnimatorStateMachine CloneStateMachine(AnimatorStateMachine src, string nameOverride = null)
    {
        if (src == null) return null;

        var dst = new AnimatorStateMachine
        {
            name = string.IsNullOrEmpty(nameOverride) ? src.name : nameOverride,
            anyStatePosition = src.anyStatePosition,
            entryPosition = src.entryPosition,
            exitPosition = src.exitPosition,
            parentStateMachinePosition = src.parentStateMachinePosition
        };

        var stateMap = new Dictionary<AnimatorState, AnimatorState>();
        var smMap = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();

        foreach (var child in src.states)
        {
            var newState = dst.AddState(child.state.name, child.position);
            EditorUtility.CopySerialized(child.state, newState);
            newState.transitions = Array.Empty<AnimatorStateTransition>();
            stateMap[child.state] = newState;
        }

        foreach (var childSM in src.stateMachines)
        {
            var newChildSM = CloneStateMachine(childSM.stateMachine);
            smMap[childSM.stateMachine] = newChildSM;
            dst.AddStateMachine(newChildSM, childSM.position);
        }

        foreach (var t in src.anyStateTransitions)
        {
            AnimatorStateTransition newT = dst.AddAnyStateTransition((AnimatorState)null);
            EditorUtility.CopySerialized(t, newT);

            newT.destinationState = t.destinationState != null && stateMap.ContainsKey(t.destinationState)
                ? stateMap[t.destinationState]
                : null;
            newT.destinationStateMachine = t.destinationStateMachine != null && smMap.ContainsKey(t.destinationStateMachine)
                ? smMap[t.destinationStateMachine]
                : null;
        }

        foreach (var t in src.entryTransitions)
        {
            AnimatorTransition newT = dst.AddEntryTransition((AnimatorState)null);
            EditorUtility.CopySerialized(t, newT);

            newT.destinationState = t.destinationState != null && stateMap.ContainsKey(t.destinationState)
                ? stateMap[t.destinationState]
                : null;
            newT.destinationStateMachine = t.destinationStateMachine != null && smMap.ContainsKey(t.destinationStateMachine)
                ? smMap[t.destinationStateMachine]
                : null;
        }

        foreach (var child in src.states)
        {
            var oldState = child.state;
            if (!stateMap.TryGetValue(oldState, out var newState)) continue;

            foreach (var t in oldState.transitions)
            {
                AnimatorStateTransition newT = newState.AddTransition((AnimatorState)null);
                EditorUtility.CopySerialized(t, newT);

                newT.destinationState = t.destinationState != null && stateMap.ContainsKey(t.destinationState)
                    ? stateMap[t.destinationState]
                    : null;
                newT.destinationStateMachine = t.destinationStateMachine != null && smMap.ContainsKey(t.destinationStateMachine)
                    ? smMap[t.destinationStateMachine]
                    : null;
            }
        }

        if (src.defaultState != null && stateMap.ContainsKey(src.defaultState))
            dst.defaultState = stateMap[src.defaultState];

        return dst;
    }

    private static void AddStateMachineSubAssetsToController(AnimatorController controller, AnimatorStateMachine sm)
    {
        if (controller == null || sm == null) return;

        if (!AssetDatabase.Contains(sm))
            AssetDatabase.AddObjectToAsset(sm, controller);

        foreach (var st in sm.states)
        {
            if (st.state != null && !AssetDatabase.Contains(st.state))
                AssetDatabase.AddObjectToAsset(st.state, controller);

            foreach (var b in st.state.behaviours)
            {
                if (b != null && !AssetDatabase.Contains(b))
                    AssetDatabase.AddObjectToAsset(b, controller);
            }
        }

        foreach (var childSM in sm.stateMachines)
        {
            if (childSM.stateMachine != null)
                AddStateMachineSubAssetsToController(controller, childSM.stateMachine);
        }
    }

    private static void RemapParametersInStateMachine(AnimatorStateMachine sm, Dictionary<string, string> map)
    {
        if (sm == null || map == null || map.Count == 0) return;

        foreach (var t in sm.anyStateTransitions)
            RemapConditions(t.conditions, map);

        foreach (var t in sm.entryTransitions)
            RemapConditions(t.conditions, map);

        foreach (var child in sm.states)
        {
            var st = child.state;
            RemapMotion(st.motion, map);

            foreach (var t in st.transitions)
                RemapConditions(t.conditions, map);
        }

        foreach (var childSM in sm.stateMachines)
            RemapParametersInStateMachine(childSM.stateMachine, map);
    }

    private static void RemapConditions(AnimatorCondition[] conditions, Dictionary<string, string> map)
    {
        if (conditions == null) return;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (map.TryGetValue(conditions[i].parameter, out var newName))
                conditions[i].parameter = newName;
        }
    }

    private static void RemapMotion(Motion motion, Dictionary<string, string> map)
    {
        if (motion == null) return;
        if (motion is BlendTree bt)
            RemapBlendTree(bt, map);
    }

    private static void RemapBlendTree(BlendTree bt, Dictionary<string, string> map)
    {
        if (bt == null) return;

        if (map.TryGetValue(bt.blendParameter, out var newBlend))
            bt.blendParameter = newBlend;

        if (map.TryGetValue(bt.blendParameterY, out var newBlendY))
            bt.blendParameterY = newBlendY;

        var children = bt.children;
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];

            if (!string.IsNullOrEmpty(child.directBlendParameter) &&
                map.TryGetValue(child.directBlendParameter, out var newDirect))
            {
                child.directBlendParameter = newDirect;
                children[i] = child;
            }

            if (child.motion is BlendTree childBT)
                RemapBlendTree(childBT, map);
        }
        bt.children = children;
    }

    // =====================================================
    // ORIGINAL PROPERTY ANIM GENERATION (UNCHANGED)
    // =====================================================

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
