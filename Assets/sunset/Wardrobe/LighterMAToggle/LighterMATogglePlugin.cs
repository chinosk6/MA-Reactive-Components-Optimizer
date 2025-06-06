
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using DefaultNamespace.sunset.Wardrobe;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using Object = UnityEngine.Object;

// NDMF Plugin. See https://github.com/bdunderscore/ndmf?tab=readme-ov-file#getting-started
[assembly: ExportsPlugin(typeof(sunset.Wardrobe.LighterMAToggle.LighterMaTogglePlugin))]
namespace sunset.Wardrobe.LighterMAToggle
{
    public class LighterMaTogglePlugin : Plugin<LighterMaTogglePlugin>
    {
        public override string QualifiedName => "dev.sunset.animator-as-code.wardrobe.lighter-ma-toggle";
        public override string DisplayName => "Sunset Lighter MA Toggle";

        private const string SystemName = "SSTLighterMaObjToggle";
        private const bool UseWriteDefaults = true;

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run($"Generate {DisplayName}", Generate);
        }

        private void Generate(BuildContext ctx)
        {
            // Find all components of type ExampleToggle in this avatar.
            var components = ctx.AvatarRootTransform.GetComponentsInChildren<LighterMaToggle>(true);
            if (components.Length == 0) return; // If there are none in the avatar, skip this entirely.
            if (components.Length > 1)
            {
                Selection.objects = components.Select(c => (Object)c.gameObject).ToArray();
                if (components.Length > 0)
                    EditorGUIUtility.PingObject(components[0].gameObject);

                var sb = new StringBuilder();
                sb.AppendLine($"Expected exactly ONE LighterMaTogglePlugin but found {components.Length}.\n"
                              + "Instances found at:\n");

                foreach (var c in components)
                {
                    sb.AppendLine($" • {c.transform.gameObject.name}");
                }
                EditorUtility.DisplayDialog($"{SystemName} Error", sb.ToString(), "OK");

                throw new Exception(sb.ToString());
                // EditorApplication.ExitPlaymode();
            }

            // Initialize Animator As Code.
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = SystemName,
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                // (For AAC 1.2.0 and above) The next line is recommended starting from NDMF 1.6.0.
                // If you use a lower version of NDMF or if you don't use it, remove that line.
                AssetContainerProvider = new NDMFContainerProvider(ctx),
                // States will be created with Write Defaults set to ON or OFF based on whether UseWriteDefaults is true or false.
                DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults)
            });

            var generatingData = GetPreGenerateWithParams(components[0], ctx.AvatarRootTransform);
            if (generatingData == null) return;
            
            // Create a new animator controller.
            // This will be merged with the rest of the playable layer at the end of this function.
            var ctrl = aac.NewAnimatorController();
            var layer = ctrl.NewLayer($"FromMA");
            var rootTree = aac.NewBlendTree().Direct();
            
            var baseWeight = layer.FloatParameter($"{SystemName}_BaseWeight");
            layer.OverrideValue(baseWeight, 1.0f);

            foreach (var (param, preGenerateItems) in generatingData)
            {
                Debug.Log($"Generating {SystemName} for param: {param}");
                
                var currentTree = GenerateMaterialSwitchAnimation(aac, layer, param, preGenerateItems, components[0]);
                rootTree.WithAnimation(currentTree, baseWeight);
                
                // 生成完成后销毁
                foreach (var preGenerateItem in preGenerateItems)
                {
                    // 不移除空 Action 的 MA Object Toggle 组件
                    if (!components[0].excludesWithEmptyClip.Contains(preGenerateItem.MaObjectToggle) && !preGenerateItem.MaObjectToggle.Inverted)
                    {
                        Object.DestroyImmediate(preGenerateItem.MaObjectToggle);
                    }
                }
            }
            Object.DestroyImmediate(components[0]);
            
            layer.NewState("MAMenuObjectToggleBlend")
                .WithAnimation(rootTree)
                .WithWriteDefaultsSetTo(true);

            // Create a new object in the scene. We will add Modular Avatar components inside it.
            var modularAvatar = MaAc.Create(new GameObject(SystemName)
            {
                transform = { parent = ctx.AvatarRootTransform }
            });
           modularAvatar.NewMergeAnimator(ctrl.AnimatorController, VRCAvatarDescriptor.AnimLayerType.FX);
        }

        private AacFlBlendTree1D GenerateMaterialSwitchAnimation(AacFlBase aac, AacFlLayer layer, string paramName, 
            List<PreGenerateObjToggleItem> preGenerateItems, LighterMaToggle lighterMaToggle)
        {
            var param = layer.FloatParameter(paramName);
            var currentTree = aac.NewBlendTree().Simple1D(param);
            
            // var threshold = 0.0f;
            
            var emptyClip = aac.NewClip($"sunset_empty_Clip");
            // currentTree.WithAnimation(emptyClip, threshold);
            // // currentTree.WithAnimation(emptyClip, 0);
            // threshold += 1.0f;

            var addEmptyZeroClip = true;
            foreach (var preGenerateItem in preGenerateItems)
            {
                // var currClip = aac.NewClip($"{paramName}_{threshold:F0}_Clip");
                var currClip = aac.NewClip($"{paramName}_{preGenerateItem.MaMenuItem.Control.value:F1}_Clip");
                if (!lighterMaToggle.excludesWithEmptyClip.Contains(preGenerateItem.MaObjectToggle) && !preGenerateItem.MaObjectToggle.Inverted)  // 使用空动画替代排除项，勾选反转条件的不处理
                {
                    foreach (var toggledObject in preGenerateItem.MaObjectToggle.Objects)
                    {
                        var avtObjRefType = typeof(AvatarObjectReference);
                        var targetObjectField = avtObjRefType.GetField("targetObject", BindingFlags.NonPublic | BindingFlags.Instance);
                        var targetObj = (GameObject)targetObjectField?.GetValue(toggledObject.Object);
                        if (targetObj == null) continue;
                    
                        currClip = currClip.Toggling(targetObj, toggledObject.Active);
                    }
                }
                // currentTree.WithAnimation(currClip, threshold);
                currentTree.WithAnimation(currClip, preGenerateItem.MaMenuItem.Control.value);
                if (preGenerateItem.MaMenuItem.Control.value == 0.0f) addEmptyZeroClip = false;
                // if (preGenerateItem.MaMenuItem.isDefault) emptyClip = currClip;
                // threshold += 1.0f;
            }

            if (addEmptyZeroClip)
            {
                currentTree.WithAnimation(emptyClip, 0);
            }
             
            return currentTree;
        }

        [CanBeNull]
        private Dictionary<string, List<PreGenerateObjToggleItem>> GetPreGenerateWithParams(LighterMaToggle lighterMaToggle, Transform avatarRootTransform)
        {
            var preGenerate = new Dictionary<string, List<PreGenerateObjToggleItem>>();
            var generatedParams = new List<string>();
            var usedParamValues = new Dictionary<string, List<float>>();
            if (!lighterMaToggle.enableOptimization) return null;

            var components = avatarRootTransform.GetComponentsInChildren<ModularAvatarObjectToggle>(true);
            
            foreach (var component in components)
            {
                if (lighterMaToggle.excludesWithNoClip.Contains(component)) continue;  // 排除项留空
                
                var materialOptimizationObject = component.gameObject;
                var maMenuItem = materialOptimizationObject.GetComponent<ModularAvatarMenuItem>();
                if (maMenuItem == null) continue;
                
                // if (maMenuItem.Control.type != VRCExpressionsMenu.Control.ControlType.Toggle)
                // {
                //     Debug.LogError($"Invalid menu item type: {maMenuItem.name} ({maMenuItem.Control.type}), expected: Toggle");
                //     continue;
                // }
                
                var paramName = maMenuItem.Control.parameter.name;
                // Debug.Log($"maMenuItem value: {paramName} {paramName == null} - {maMenuItem.automaticValue}, {maMenuItem.Control.value}");

                if (paramName == null)
                {
                    // maMenuItem.Control = new VRCExpressionsMenu.Control();
                    // maMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    maMenuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter();
                    maMenuItem.Control.parameter.name = "";
                    paramName = "";
                }
                
                if (paramName == "")
                {
                    // Debug.LogError();
                    var currGenerateParamName = $"__Sunset_Generated_{SystemName}_{maMenuItem.name}";
                    while (generatedParams.Contains(currGenerateParamName))
                    {
                        currGenerateParamName = $"__Sunset_Generated_{SystemName}_{maMenuItem.name}_{SunsetUtils.GenerateRandomSTring(6)}";
                    }
                    generatedParams.Add(currGenerateParamName);
                    maMenuItem.Control.parameter.name = currGenerateParamName;
                    paramName = currGenerateParamName;
                }

                if (!maMenuItem.automaticValue)  // 记录手动填写的值
                {
                    if (usedParamValues.ContainsKey(paramName))
                    {
                        usedParamValues[paramName].Add(maMenuItem.Control.value);
                    }
                    else
                    {
                        usedParamValues.Add(paramName, new List<float> { maMenuItem.Control.value });
                    }
                }
                if (preGenerate.ContainsKey(paramName))
                {
                    preGenerate[paramName].Add(new PreGenerateObjToggleItem(component, maMenuItem));
                }
                else
                {
                    preGenerate.Add(paramName, new List<PreGenerateObjToggleItem>(){ new (component, maMenuItem) });
                }
            }
            
            // 赋值自动值，排除手动填写已经用过的值
            foreach (var (key, items) in preGenerate)
            {
                var threshold = 1.0f;
                foreach (var item in items)
                {
                    if (!item.MaMenuItem.automaticValue) continue;
                    
                    if (!usedParamValues.TryGetValue(key, out var usedParamValue))
                    {
                        usedParamValue = new List<float>();
                    }
                    // Default 开，且不是 bool 开关，将 default 选项值设置为 0
                    if (item.MaMenuItem.isDefault && (items.Count > 1) && !usedParamValue.Contains(0.0f))
                    {
                        item.MaMenuItem.automaticValue = false;
                        item.MaMenuItem.Control.value = 0.0f;
                        continue;
                    }
                    while (usedParamValue.Contains(threshold)) threshold += 1.0f;

                    item.MaMenuItem.automaticValue = false;
                    item.MaMenuItem.Control.value = threshold;
                    threshold += 1.0f;
                }
            }

            return preGenerate;
        }
    }

    internal class PreGenerateObjToggleItem
    {
        public ModularAvatarMenuItem MaMenuItem { get; set; }
        public ModularAvatarObjectToggle MaObjectToggle { get; set; }

        public PreGenerateObjToggleItem(ModularAvatarObjectToggle maObjectToggle, ModularAvatarMenuItem maMenuItem)
        {
            MaMenuItem = maMenuItem;
            MaObjectToggle = maObjectToggle;
        }
    }

    // (For AAC 1.2.0 and above) This is recommended starting from NDMF 1.6.0. You only need to define this class once.
    internal class NDMFContainerProvider : IAacAssetContainerProvider
    {
        private readonly BuildContext _ctx;
        public NDMFContainerProvider(BuildContext ctx) => _ctx = ctx;
        public void SaveAsPersistenceRequired(Object objectToAdd) => _ctx.AssetSaver.SaveAsset(objectToAdd);
        public void SaveAsRegular(Object objectToAdd) { } // Let NDMF crawl our assets when it finishes
        public void ClearPreviousAssets() { } // ClearPreviousAssets is never used in non-destructive contexts
    }
}
#endif