// Copyright 2017-2020 Elringus (Artyom Sovetnikov). All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UniRx.Async;
using UnityEngine;

namespace Naninovel
{
    /// <inheritdoc cref="ILocalizationManager"/>
    [InitializeAtRuntime]
    public class LocalizationManager : IStatefulService<SettingsStateMap>, ILocalizationManager
    {
        [Serializable]
        public class Settings
        {
            public string SelectedLocale;
        }

        public event Action<string> OnLocaleChanged;

        public virtual LocalizationConfiguration Configuration { get; }
        public virtual string SelectedLocale { get; private set; }

        protected virtual List<string> AvailableLocales { get; } = new List<string>();

        private readonly IResourceProviderManager providersManager;
        private readonly HashSet<Func<UniTask>> changeLocaleTasks = new HashSet<Func<UniTask>>();
        private List<IResourceProvider> providerList;

        public LocalizationManager (LocalizationConfiguration config, IResourceProviderManager providersManager)
        {
            Configuration = config;
            this.providersManager = providersManager;
        }

        public virtual async UniTask InitializeServiceAsync ()
        {
            providerList = providersManager.GetProviders(Configuration.Loader.ProviderTypes);
            await RetrieveAvailableLocalesAsync();
        }

        public virtual void ResetService () { }

        public virtual void DestroyService () { }

        public virtual void SaveServiceState (SettingsStateMap stateMap)
        {
            var settings = new Settings() {
                SelectedLocale = SelectedLocale
            };
            stateMap.SetState(settings);
        }

        public virtual async UniTask LoadServiceStateAsync (SettingsStateMap stateMap)
        {
            var defaultLocale = string.IsNullOrEmpty(Configuration.DefaultLocale) ? Configuration.SourceLocale : Configuration.DefaultLocale;
            var settings = stateMap.GetState<Settings>() ?? new Settings { SelectedLocale = defaultLocale };
            await SelectLocaleAsync(settings.SelectedLocale ?? defaultLocale);
        }

        public virtual IEnumerable<string> GetAvailableLocales () => AvailableLocales.ToArray();

        public virtual bool LocaleAvailable (string locale) => AvailableLocales.Contains(locale);

        public virtual async UniTask SelectLocaleAsync (string locale)
        {
            if (!LocaleAvailable(locale))
            {
                Debug.LogWarning($"Failed to select locale: Locale `{locale}` is not available.");
                return;
            }

            if (locale == SelectedLocale) return;

            SelectedLocale = locale;

            foreach (var task in changeLocaleTasks)
                await task();

            OnLocaleChanged?.Invoke(SelectedLocale);
        }

        public virtual void AddChangeLocaleTask (Func<UniTask> taskFunc) => changeLocaleTasks.Add(taskFunc);

        public virtual void RemoveChangeLocaleTask (Func<UniTask> taskFunc) => changeLocaleTasks.Remove(taskFunc);

        public virtual async UniTask<bool> LocalizedResourceAvailableAsync<TResource> (string path) where TResource : UnityEngine.Object
        {
            if (SelectedLocale == Configuration.SourceLocale) return false;
            var localizedResourcePath = BuildLocalizedResourcePath(path);
            return await providerList.ResourceExistsAsync<TResource>(localizedResourcePath);
        }

        public virtual async UniTask<Resource<TResource>> LoadLocalizedResourceAsync<TResource> (string path) where TResource : UnityEngine.Object
        {
            var localizedResourcePath = BuildLocalizedResourcePath(path);
            return await providerList.LoadResourceAsync<TResource>(localizedResourcePath);
        }

        public virtual Resource<TResource> GetLoadedLocalizedResourceOrNull<TResource> (string path) where TResource : UnityEngine.Object
        {
            var localizedResourcePath = BuildLocalizedResourcePath(path);
            return providerList.GetLoadedResourceOrNull<TResource>(localizedResourcePath);
        }

        public virtual void UnloadLocalizedResource (string path)
        {
            var localizedResourcePath = BuildLocalizedResourcePath(path);
            providerList.UnloadResource(localizedResourcePath);
        }

        public virtual bool LocalizedResourceLoaded (string path)
        {
            var localizedResourcePath = BuildLocalizedResourcePath(path);
            return providerList.ResourceLoaded(localizedResourcePath);
        }

        /// <summary>
        /// Retrieves available localizations by locating folders inside the localization resources root.
        /// Folder names should correspond to the <see cref="LanguageTags"/> tag entries (RFC5646).
        /// </summary>
        private async UniTask RetrieveAvailableLocalesAsync ()
        {
            var resources = await providerList.LocateFoldersAsync(Configuration.Loader.PathPrefix);
            AvailableLocales.Clear();
            AvailableLocales.AddRange(resources.Select(r => r.Name).Where(tag => LanguageTags.ContainsTag(tag)));
            AvailableLocales.Add(Configuration.SourceLocale);
        }

        private string BuildLocalizedResourcePath (string resourcePath) => $"{Configuration.Loader.PathPrefix}/{SelectedLocale}/{resourcePath}";
    }
}
