using System;
using JetBrains.Annotations;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.Helpers
{
    public static class ComponentExtensions
    {
        /// <summary>
        /// Gets the specified component if it exists on the GameObject; otherwise, adds it.
        /// </summary>
        /// <typeparam name="T">The type of the component.</typeparam>
        /// <param name="go">The GameObject to get or add the component to.</param>
        /// <returns>The existing or newly added component.</returns>
        [PublicAPI]
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (!component) component = go.AddComponent<T>();

            return component;
        }
    }
}