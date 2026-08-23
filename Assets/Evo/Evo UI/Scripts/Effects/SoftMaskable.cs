using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [RequireComponent(typeof(Graphic))]
    public class SoftMaskable : UIBehaviour, IMaterialModifier
    {
        public SoftMask AssignedMask { get; private set; }
        Graphic graphic;

        protected override void Awake()
        {
            base.Awake();
            graphic = GetComponent<Graphic>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // graphic will already be populated by Awake in most cases, 
            // keeping this null check prevents redundant GetComponent calls.
            if (graphic == null)
                graphic = GetComponent<Graphic>();

            FindMask();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            FindMask();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (graphic != null)
                graphic.SetMaterialDirty();
        }

        void FindMask()
        {
            AssignedMask = GetComponentInParent<SoftMask>();

            if (graphic != null)
                graphic.SetMaterialDirty();
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (AssignedMask != null && AssignedMask.isActiveAndEnabled)
            {
                if (graphic is MaskableGraphic maskableGraphic && !maskableGraphic.maskable)
                    return baseMaterial;

                return AssignedMask.GetModifiedMaterialForChild(baseMaterial);
            }

            return baseMaterial;
        }
    }
}