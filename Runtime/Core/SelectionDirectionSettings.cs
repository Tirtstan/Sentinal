using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Authored search and preferred-target settings for one navigation direction.
    /// </summary>
    [Serializable]
    public sealed class SelectionDirectionSettings
    {
        [SerializeField]
        [Tooltip("Valid targets are selected in this order before automatic spatial search.")]
        private List<SelectionNavigator> preferredTargets = new();

        [SerializeField]
        [Tooltip("Overrides the navigator's default search angle for this direction.")]
        private bool overrideSearchAngle;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Maximum angle in degrees from this direction for automatic candidates. 0 disables automatic search.")]
        private float searchAngle = SelectionNavigator.DefaultSearchAngleValue;

        [NonSerialized]
        private ReadOnlyCollection<SelectionNavigator> readOnlyPreferredTargets;

        /// <summary>
        /// Valid targets selected in order before automatic spatial search.
        /// </summary>
        public IReadOnlyList<SelectionNavigator> PreferredTargets =>
            readOnlyPreferredTargets ??= preferredTargets.AsReadOnly();

        /// <summary>
        /// Whether <see cref="SearchAngle"/> replaces the navigator's default search angle.
        /// </summary>
        public bool OverridesSearchAngle => overrideSearchAngle;

        /// <summary>
        /// The search angle used when <see cref="OverridesSearchAngle"/> is true.
        /// </summary>
        public float SearchAngle => searchAngle;

        public void SetSearchAngle(float angle)
        {
            overrideSearchAngle = true;
            searchAngle = Mathf.Clamp(angle, 0f, 180f);
        }

        public void ClearSearchAngle()
        {
            overrideSearchAngle = false;
            searchAngle = SelectionNavigator.DefaultSearchAngleValue;
        }

        public void ReplacePreferredTargets(IReadOnlyList<SelectionNavigator> targets)
        {
            preferredTargets.Clear();

            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
                preferredTargets.Add(targets[i]);
        }

        internal void Normalize() => searchAngle = Mathf.Clamp(searchAngle, 0f, 180f);

        internal List<SelectionNavigator> GetMutablePreferredTargets() => preferredTargets;
    }
}
