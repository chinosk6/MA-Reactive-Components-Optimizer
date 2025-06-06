using UnityEngine;
using VRC.SDKBase;

#if UNITY_EDITOR

namespace sunset.Wardrobe.MaterialChanger
{
    [DisallowMultipleComponent]
    [AddComponentMenu("sunset/MA Material Setter Optimization")]
    public class MaterialChangerModel : MonoBehaviour, IEditorOnly
    {
        public bool enableOptimization = true;
    }
}

#endif