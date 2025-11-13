# CCK Enhancement Tool for ChilloutVR

A powerful Unity editor tool that streamlines the creation of Advanced Avatar Settings for ChilloutVR avatars by automating animation clip generation, animator merging, and avatar parameter setup.

## Table of Contents
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Usage Guide](#usage-guide)
  - [1. Animator Merging](#1-animator-merging)
  - [2. Property Animation Generation](#2-property-animation-generation)
  - [3. Multi-Clip Merging](#3-multi-clip-merging)
- [Examples](#examples)
- [Why Use This Tool?](#why-use-this-tool)
- [Technical Details](#technical-details)

## Features

✨ **Animator Merging**
- Automatically merge multiple animator controllers into your CVR base controller
- Creates timestamped backups before merging
- Handles duplicate layer names intelligently
- Preserves all parameters and layers

🎨 **Property Animation Generation**
- Generate animation clips and avatar entries from GameObject properties
- Support for material properties across multiple objects
- Animate colors, floats, integers, and booleans
- Choose between Toggle, Slider, Dropdown, or Color picker settings
- Custom value ranges for precise control

🔧 **Material Property Support**
- Find all objects using a specific material automatically
- Select/deselect which objects to animate
- Animate shader properties like emission, colors, metallic, etc.
- Support for color gradients and value ranges

📋 **Multi-Clip Merging**
- Combine multiple animation clips into a single avatar entry
- Perfect for organizing complex animation sequences

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/CCKEnhancement/releases) page
2. Extract the files to your Unity project
3. Place `CCKEnhancementComponent.cs` in your `Assets` folder
4. Place `CCKEnhancementEditor.cs` in your `Assets/Editor` folder

**Requirements:**
- Unity 2021.3 or higher (ChilloutVR supported version)
- ChilloutVR CCK installed

## Quick Start

### Using as Editor Window
1. Open **CVR Tools > CCK Enhancement** from the Unity menu
2. Drag your avatar root into the "Avatar Root" field
3. Use the three main sections to enhance your avatar

### Using as Component
1. Select your avatar root GameObject
2. Add Component > **CVR Tools > CCK Enhancement Component**
3. Configure settings directly in the Inspector

## Usage Guide

### 1. Animator Merging

**Purpose:** Merge multiple animator controllers into your CVR Advanced Avatar base controller without manual copying.

**Steps:**
1. Ensure your avatar has Advanced Avatar Settings initialized
2. Click "Add Animator Controller" to add controllers to merge
3. Drag animator controllers into the slots
4. Click "Merge Animators"

**Result:** 
- All animators are merged into the base controller
- Automatic backup created in `Assets/AdvancedSettings.Generated/Backups/`
- Duplicate layers are renamed automatically (e.g., `Layer_1`, `Layer_2`)

### 2. Property Animation Generation

**Purpose:** Automatically generate animation clips and avatar parameters from GameObject or Material properties.

#### GameObject Properties

**Steps:**
1. Leave "Use Material Property" unchecked
2. Select your target GameObject
3. Choose the property to animate from the dropdown
4. Set the "Advanced Avatar Property Name"
5. Choose a "Settings Type" (Toggle, Slider, Dropdown, or Color)
6. Set value ranges (e.g., Float Value 0: 0, Float Value 1: 1)
7. Click "Generate Animations + Avatar Entries"

**Example - Toggle Light:**
- Target Object: `Light`
- Property: `Light/m_Intensity`
- Property Type: `Float`
- Settings Type: `Toggle`
- Float Value 0: `0` (off)
- Float Value 1: `1` (on)

#### Material Properties

**Steps:**
1. Check "Use Material Property"
2. Select your material
3. Click "Find Objects with Material"
4. Select/deselect which objects to animate
5. Choose the material property (e.g., `material._EmissionColor`)
6. Choose Settings Type
7. Set color or value ranges
8. Click "Generate Animations + Avatar Entries"

**Example - Emission Color Toggle:**
- Material: `MyEmissiveMaterial`
- Property: `material._EmissionColor (Color)`
- Settings Type: `Toggle`
- Color Value 0: `Black` (off)
- Color Value 1: `White` (on)
- Found Objects: 15 meshes selected

**Example - Emission Strength Slider:**
- Material: `MyEmissiveMaterial`
- Property: `material._EmissionStrength`
- Settings Type: `Slider`
- Float Value 0: `0`
- Float Value 1: `5`

**Example - Color Gradient Dropdown:**
- Material: `MyMaterial`
- Property: `material._Color (Color)`
- Settings Type: `Dropdown`
- Number of Options: `5`
- Creates color gradient from Color 0 to Color 1 across 5 options

### 3. Multi-Clip Merging

**Purpose:** Combine existing animation clips into a single avatar parameter entry.

**Steps:**
1. Click "Add Multi Clip Merge Entry"
2. Set the "Advanced Avatar Property Name"
3. Choose Property Type
4. Click "Add Animation Clip" and add your clips
5. Click "Merge Clips to Avatar Entries"

**Example - Gesture Animations:**
- Combine 8 hand gesture animation clips
- Create one dropdown with all gestures
- Easier to manage than individual entries

## Examples

### Example 1: Toggle Multiple Lights

**Before CCK Enhancement:**
- Create 2 animation clips manually (lights on/off)
- Set up advanced avatar toggle entry
- Configure animation clip references
- Test and debug
- **Time: ~10 minutes per light**

**With CCK Enhancement:**
1. Select light GameObject
2. Choose `Light/m_Intensity` property
3. Set Toggle with values 0 to 1
4. Generate
- **Time: ~30 seconds per light**

**Improvement: 95% faster** ⚡

### Example 2: Animate Emission on 50 Objects

**Before CCK Enhancement:**
- Manually find all 50 objects with the material
- Create 2 animation clips
- Add all 50 objects to each clip
- Set emission curves for each
- Set up avatar entry
- **Time: ~2 hours**

**With CCK Enhancement:**
1. Select material
2. Click "Find Objects with Material" → finds all 50
3. Choose `_EmissionColor` property
4. Set color range
5. Generate
- **Time: ~2 minutes**

**Improvement: 98% faster, 100% less error-prone** 🎯

### Example 3: Color Picker for Custom Shader

**Before CCK Enhancement:**
- Create material color parameter
- Set up advanced avatar color entry
- Configure shader property binding
- **Time: ~5 minutes**

**With CCK Enhancement:**
1. Select material
2. Choose color property
3. Set Settings Type to "Color"
4. Generate
- **Time: ~30 seconds**

**Improvement: 90% faster** 🌈

### Example 4: Smooth Metallic Slider

**Before CCK Enhancement:**
- Create 2 animation clips (min/max metallic)
- Set up slider entry
- Configure float range
- **Time: ~8 minutes**

**With CCK Enhancement:**
1. Select material
2. Choose `_Metallic` property
3. Set Slider from 0 to 1
4. Generate
- **Time: ~45 seconds**

**Improvement: 91% faster** ✨

## Why Use This Tool?

### Time Savings
- **Animation Creation:** Automatically generates clips with proper curves
- **Bulk Operations:** Animate properties across dozens of objects simultaneously
- **No Manual Entry:** Avatar settings created with proper configuration

### Error Reduction
- **No Missing References:** Clips automatically linked to avatar entries
- **Correct Paths:** Animation paths generated accurately
- **Type Safety:** Property types validated before generation

### Workflow Improvements
- **Iterative Development:** Quickly test different value ranges
- **Material Variants:** Easily create multiple color/property variants
- **Prototyping:** Rapid creation of avatar parameters for testing

### Organization
- **Automatic Backups:** Never lose your animator setup
- **Naming Conventions:** Consistent file and parameter naming
- **Folder Structure:** Generated assets organized in dedicated folder

## Technical Details

### Generated File Structure
```
Assets/
├── AdvancedSettings.Generated/
│   ├── Backups/
│   │   └── BaseController_Backup_20251113_001500.controller
│   ├── AvatarName_PropertyName_Off.anim
│   ├── AvatarName_PropertyName_On.anim
│   └── AvatarName_MergedClip.anim
```

### Supported Property Types
- **Bool:** True/false toggles
- **Float:** Numeric sliders or inputs
- **Int:** Integer values for dropdowns
- **Color:** RGBA color values

### Supported Settings Types
- **Toggle:** On/off switch (2 animation clips)
- **Slider:** Continuous range (2 animation clips: min/max)
- **Dropdown:** Multiple options (n animation clips)
- **Color:** In-game color picker (no animation clips)

### Supported Material Property Types
- Float (e.g., `_Metallic`, `_Smoothness`)
- Range (e.g., `_Glossiness`)
- Color (e.g., `_Color`, `_EmissionColor`)
- Vector (e.g., `_MainTex_ST`)

## Advanced Features

### Reverse Generation
For boolean toggles, you can swap the on/off states:
- Useful when you want "enabled" to mean "off"
- Example: Inverting a light intensity toggle

### Dropdown Value Interpolation
When creating dropdowns with numeric properties:
- Values are automatically interpolated between min and max
- 5 options from 0 to 10 → [0, 2.5, 5, 7.5, 10]

### Selective Object Animation
For material properties:
- Found objects can be individually enabled/disabled
- Useful for excluding specific meshes from animation

### Animator Backup System
- Timestamped backups: `Controller_Backup_YYYYMMDD_HHMMSS.controller`
- Prevents data loss during merging operations
- Easy rollback if something goes wrong

## Tips & Best Practices

1. **Test in Play Mode:** Always test generated animations in play mode before uploading
2. **Use Descriptive Names:** Name your properties clearly for easier management
3. **Check Material Slots:** Ensure materials are in the correct slot when animating
4. **Backup Manually:** Keep manual backups of important animators before major changes
5. **Start Simple:** Begin with toggle properties before moving to complex sliders/dropdowns

## Troubleshooting

**Animation clips not working:**
- Verify GameObject paths are correct
- Check that component/property names match exactly
- Ensure avatar root is set correctly

**Material properties not found:**
- Verify the shader exposes the property
- Check if property is marked as animatable in the shader
- Some shader properties may not be accessible

**Merge failed:**
- Ensure Advanced Avatar Settings are initialized first
- Check that base controller exists and is valid
- Verify you have write permissions to the folder

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.
