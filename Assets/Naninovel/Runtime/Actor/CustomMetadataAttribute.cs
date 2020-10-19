// Copyright 2017-2020 Elringus (Artyom Sovetnikov). All Rights Reserved.

using System;

namespace Naninovel
{
    /// <summary>
    /// When applied to a <see cref="UnityEngine.ScriptableObject"/>, serializable fields will be added to <see cref="ActorMetadata"/> for the actors of the specified implementation.
    /// The data will be exposed in the Naninovel's actor editor menus and can be accessed at runtime with <see cref="ActorMetadata.GetCustomData{TData}"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CustomMetadataAttribute : Attribute
    {
        public Type ImplementationType { get; }

        /// <param name="implementationType">Actor implementation type for which to add custom metadata.</param>
        public CustomMetadataAttribute (Type implementationType)
        {
            ImplementationType = implementationType;
        }
    }
}
