// Copyright 2017-2020 Elringus (Artyom Sovetnikov). All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using UniRx.Async;

namespace Naninovel
{
    /// <summary>
    /// A <see cref="ResourceLoader{TResource}"/>, that will attempt to use <see cref="Naninovel.ILocalizationManager"/> to retrieve localized versions 
    /// of the requested resources and (optionally) fallback to the source (original) versions when localized versions are not available.
    /// </summary>
    public class LocalizableResourceLoader<TResource> : ResourceLoader<TResource>
        where TResource : UnityEngine.Object
    {
        protected readonly ILocalizationManager LocalizationManager;
        protected readonly List<IResourceProvider> SourceProviders;
        protected readonly string SourcePrefix;
        protected readonly bool FallbackToSource;
        
        /// <param name="providersList">Prioritized list of the source providers.</param>
        /// <param name="localizationManager">Localization manager instance.</param>
        /// <param name="sourcePrefix">Resource path prefix for the source providers.</param>
        /// <param name="fallbackToSource">Whether to fallback to the source versions of the resources when localized versions are not available.</param>
        public LocalizableResourceLoader (List<IResourceProvider> providersList, ILocalizationManager localizationManager, 
            string sourcePrefix = null, bool fallbackToSource = true) : base(providersList, sourcePrefix)
        {
            LocalizationManager = localizationManager;
            SourceProviders = providersList.ToList();
            SourcePrefix = sourcePrefix;
            FallbackToSource = fallbackToSource;
            
            LocalizationManager.AddChangeLocaleTask(InitializeProvisionSources);
            InitializeProvisionSources();
        }

        ~LocalizableResourceLoader ()
        {
            LocalizationManager?.RemoveChangeLocaleTask(InitializeProvisionSources);
        }

        protected UniTask InitializeProvisionSources ()
        {
            UnloadAll();
            
            ProvisionSources.Clear();

            if (!LocalizationManager.IsSourceLocaleSelected())
            {
                var localePrefix = $"{LocalizationManager.Configuration.Loader.PathPrefix}/{LocalizationManager.SelectedLocale}/{SourcePrefix}";
                foreach (var provider in LocalizationManager.ProviderList)
                    ProvisionSources.Add(new ProvisionSource(provider, localePrefix));
            }

            if (FallbackToSource)
                foreach (var provider in SourceProviders)
                    ProvisionSources.Add(new ProvisionSource(provider, SourcePrefix));
            
            return UniTask.CompletedTask;
        }
    }
}
