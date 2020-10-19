// Copyright 2017-2020 Elringus (Artyom Sovetnikov). All Rights Reserved.

using UnityEngine;

namespace Naninovel
{
    [System.Serializable]
    public class UIConfiguration : Configuration
    {
        public const string DefaultPathPrefix = "UI";

        [Tooltip("Configuration of the resource loader used with UI resources.")]
        public ResourceLoaderConfiguration Loader = new ResourceLoaderConfiguration { PathPrefix = DefaultPathPrefix };
        [Tooltip("Whether to assign a specific layer to all the UI objects managed by the engine. Required for some of the built-in features, eg `Toggle UI`.")]
        public bool OverrideObjectsLayer = true;
        [Tooltip("When `Override Objects Layer` is enabled, the specified layer will be assigned to all the engine objects.")]
        public int ObjectsLayer = 5;
        [Tooltip("The canvas render mode to apply for all the managed UI elements.")]
        public RenderMode RenderMode = RenderMode.ScreenSpaceCamera;
        [Tooltip("The sorting offset to apply for all the managed UI elements.")]
        public int SortingOffset = 1;
    }
}
