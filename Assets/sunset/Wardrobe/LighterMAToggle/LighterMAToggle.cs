using UnityEngine;
using VRC.SDKBase;
using nadena.dev.modular_avatar.core;
using UnityEngine.Serialization;

#if UNITY_EDITOR

namespace sunset.Wardrobe.LighterMAToggle
{
    [DisallowMultipleComponent]
    [AddComponentMenu("sunset/MA Menu Object Toggle Optimization")]
    public class LighterMaToggle : MonoBehaviour, IEditorOnly
    {
        public bool enableOptimization = true;
        
        [FormerlySerializedAs("excludesWithEmptyAction")]
        public ModularAvatarObjectToggle[] excludesWithEmptyClip;
        
        [FormerlySerializedAs("excludesWithNoAction")]
        public ModularAvatarObjectToggle[] excludesWithNoClip;
    }
}

#endif