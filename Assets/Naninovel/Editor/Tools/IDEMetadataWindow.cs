// Copyright 2017-2020 Elringus (Artyom Sovetnikov). All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Naninovel.Commands;

namespace Naninovel
{
    public class IDEMetadataWindow : EditorWindow
    {
        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class ProjectMetadata
        {
            public List<CommandMetadata> commands;
            public List<ProjectResource> resources;
            public List<ProjectActor> actors;
        }
        
        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class ProjectResource
        {
            public string name;
            public string pathPrefix;
        }
        
        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class ProjectActor
        {
            public string id;
            public string pathPrefix;
            public string displayName;
            public List<string> appearances;
        }
        
        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class CommandMetadata
        {
            public string id;
            public string alias;
            public bool localizable;
            public string summary;
            public string remarks;
            public List<ParameterMetadata> @params;
        }

        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class ParameterMetadata
        {
            public string id;
            public string alias;
            public bool nameless;
            public bool required;
            public DataType dataType;
            public string summary;
            public string resourcePathPrefix;
            public int resourcePathPrefixNamedId;
            public string actorPathPrefix;
            public int actorPathPrefixNamedId;
            public bool appearance;
            public int appearanceNamedId;
            public string constant;
            public int constantNamedId;
        }

        [Serializable, SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private class DataType
        {
            public string kind;
            public string contentType;
        }
        
        protected bool GenerateCommands
        {
            get => PlayerPrefs.GetInt(generateCommandsKey, 1) == 1;
            set => PlayerPrefs.SetInt(generateCommandsKey, value ? 1 : 0);
        }
        protected bool GenerateResources
        {
            get => PlayerPrefs.GetInt(generateResourcesKey, 1) == 1;
            set => PlayerPrefs.SetInt(generateResourcesKey, value ? 1 : 0);
        }
        protected bool GenerateActors
        {
            get => PlayerPrefs.GetInt(generateActorsKey, 1) == 1;
            set => PlayerPrefs.SetInt(generateActorsKey, value ? 1 : 0);
        }
        protected string OutputPath
        {
            get => PlayerPrefs.GetString(outputPathKey);
            set { PlayerPrefs.SetString(outputPathKey, value); ValidateOutputPath(); }
        }

        private static readonly GUIContent generateCommandsContent = new GUIContent("Generate Commands", "Whether to generate metadata for the custom commands.");
        private static readonly GUIContent generateResourcesContent = new GUIContent("Generate Resources", "Whether to generate metadata for the project resources.");
        private static readonly GUIContent generateActorsContent = new GUIContent("Generate Actors", "Whether to generate metadata for the actors.");
        private static readonly GUIContent outputPathContent = new GUIContent("Output Path", $"Path to `{outputFolderName}` folder of the target Naninovel IDE extension.");

        private const string generateCommandsKey = "Naninovel." + nameof(IDEMetadataWindow) + "." + nameof(GenerateCommands);
        private const string generateResourcesKey = "Naninovel." + nameof(IDEMetadataWindow) + "." + nameof(GenerateResources);
        private const string generateActorsKey = "Naninovel." + nameof(IDEMetadataWindow) + "." + nameof(GenerateActors);
        private const string outputPathKey = "Naninovel." + nameof(IDEMetadataWindow) + "." + nameof(OutputPath);
        private const string outputFolderName = "server";
        private const string fileName = "customMetadata.json";
        private const string contentTemplate = "{\"commands\": [\n\n{0}]}";

        private bool isWorking = false;
        private bool outputPathValid = false;

        [MenuItem("Naninovel/Tools/IDE Metadata")]
        public static void OpenWindow ()
        {
            var position = new Rect(100, 100, 500, 135);
            GetWindowWithRect<IDEMetadataWindow>(position, true, "IDE Metadata", true);
        }

        private void OnEnable ()
        {
            ValidateOutputPath();
        }

        private void ValidateOutputPath ()
        {
            outputPathValid = OutputPath?.EndsWith(outputFolderName) ?? false;
        }

        private void OnGUI ()
        {
            EditorGUILayout.LabelField("Naninovel IDE Metadata", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The tool to generate IDE metadata; see `IDE Extension` guide for usage instructions.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            if (isWorking)
            {
                EditorGUILayout.HelpBox("Working, please wait...", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                OutputPath = EditorGUILayout.TextField(outputPathContent, OutputPath);
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(65)))
                    OutputPath = EditorUtility.OpenFolderPanel("Output Path", "", "");
            }

            GUILayout.FlexibleSpace();

            if (!outputPathValid)
                EditorGUILayout.HelpBox($"Output path is not valid. Make sure it points to a `{outputFolderName}` folder under a Naninovel IDE extension installation directory.", MessageType.Error);
            else if (GUILayout.Button("Generate IDE Metadata", GUIStyles.NavigationButton))
                GenerateMetadata();

            EditorGUILayout.Space();
        }

        private void GenerateMetadata ()
        {
            try
            {
                isWorking = true;
                EditorUtility.DisplayProgressBar("Generating IDE Metadata", "Processing commands...", 0);
                var projectMeta = new ProjectMetadata();
                if (GenerateCommands)
                    GenerateCustomCommandsMetadata(projectMeta);
                EditorUtility.DisplayProgressBar("Generating IDE Metadata", "Processing resources...", .5f);
                if (GenerateResources)
                    GenerateResourcesMetadata(projectMeta);
                EditorUtility.DisplayProgressBar("Generating IDE Metadata", "Processing actors...", .75f);
                if (GenerateActors)
                    GenerateActorsMetadata(projectMeta);
                WriteFile(projectMeta);
            }
            finally
            {
                isWorking = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void WriteFile (ProjectMetadata projectMeta)
        {
            var fullPath = Path.Combine(OutputPath, fileName);
            var json = JsonUtility.ToJson(projectMeta, true);
            File.WriteAllText(fullPath, json);
        }

        private void GenerateResourcesMetadata (ProjectMetadata projectMeta)
        {
            var resources = new List<ProjectResource>();
            var editorResources = EditorResources.LoadOrDefault();
            var records = editorResources.GetAllRecords();
            foreach (var kv in records)
            {
                var record = editorResources.GetRecordByGuid(kv.Value);
                if (record is null) continue;
                var resource = new ProjectResource
                {
                    pathPrefix = record.Value.PathPrefix,
                    name = record.Value.Name
                };
                resources.Add(resource);
            }
            projectMeta.resources = resources;
        }
        
        private void GenerateActorsMetadata (ProjectMetadata projectMeta)
        {
            var actors = new List<ProjectActor>();
            var editorResources = EditorResources.LoadOrDefault();
            var allResources = editorResources.GetAllRecords().Keys.ToArray();
            var chars = ProjectConfigurationProvider.LoadOrDefault<CharactersConfiguration>().Metadata.ToDictionary();
            foreach (var kv in chars)
            {
                var charActor = new ProjectActor
                {
                    id = kv.Key,
                    displayName = kv.Value.DisplayName,
                    pathPrefix = kv.Value.Loader.PathPrefix,
                    appearances = FindAppearances(kv.Key, kv.Value.Loader.PathPrefix, kv.Value.Implementation)
                };
                actors.Add(charActor);
            }
            var backs = ProjectConfigurationProvider.LoadOrDefault<BackgroundsConfiguration>().Metadata.ToDictionary();
            foreach (var kv in backs)
            {
                var backActor = new ProjectActor
                {
                    id = kv.Key,
                    pathPrefix = kv.Value.Loader.PathPrefix,
                    appearances = FindAppearances(kv.Key, kv.Value.Loader.PathPrefix, kv.Value.Implementation)
                };
                actors.Add(backActor);
            }
            var choiceHandlers = ProjectConfigurationProvider.LoadOrDefault<ChoiceHandlersConfiguration>().Metadata.ToDictionary();
            foreach (var kv in choiceHandlers)
            {
                var choiceHandlerActor = new ProjectActor
                {
                    id = kv.Key,
                    pathPrefix = kv.Value.Loader.PathPrefix
                };
                actors.Add(choiceHandlerActor);
            }
            var printers = ProjectConfigurationProvider.LoadOrDefault<TextPrintersConfiguration>().Metadata.ToDictionary();
            foreach (var kv in printers)
            {
                var printerActor = new ProjectActor
                {
                    id = kv.Key,
                    pathPrefix = kv.Value.Loader.PathPrefix
                };
                actors.Add(printerActor);
            }
            projectMeta.actors = actors;

            List<string> FindAppearances (string actorId, string pathPrefix, string actorImplementation)
            {
                var prefabPath = allResources.FirstOrDefault(p => p.EndsWithFast($"{pathPrefix}/{actorId}"));
                var assetGUID = prefabPath != null ? editorResources.GetGuidByPath(prefabPath) : null;
                var assetPath = assetGUID != null ? AssetDatabase.GUIDToAssetPath(assetGUID) : null;
                var prefabAsset = assetPath != null ? AssetDatabase.LoadMainAssetAtPath(assetPath) : null;
                if (prefabAsset != null && actorImplementation.Contains("Layered"))
                {
                    var layeredBehaviour = (prefabAsset as GameObject)?.GetComponent<LayeredActorBehaviour>();
                    return layeredBehaviour != null ? layeredBehaviour.CompositionMap.Keys.ToList() : new List<string>();
                }
                else if (prefabAsset != null && (actorImplementation.Contains("Generic") || actorImplementation.Contains("Live2D")))
                {
                    var animator = (prefabAsset as GameObject)?.GetComponent<Animator>();
                    var controller = animator != null ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
                    return controller != null ? controller.parameters
                        .Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name).ToList() : new List<string>();
                }
                #if SPRITE_DICING_AVAILABLE
                else if (prefabAsset != null && actorImplementation.Contains("Diced"))
                {
                    return (prefabAsset as SpriteDicing.DicedSpriteAtlas)?.GetAllSprites().Select(s => s.name).ToList() ?? new List<string>();
                }
                #endif
                else
                {
                    var multiplePrefix = $"{pathPrefix}/{actorId}/";
                    return allResources.Where(p => p.Contains(multiplePrefix)).Select(p => p.GetAfter(multiplePrefix)).ToList();
                }
            }
        }

        private void GenerateCustomCommandsMetadata (ProjectMetadata projectMeta)
        {
            projectMeta.commands = new List<CommandMetadata>();
            var customCommandTypes = Command.CommandTypes.Values.Where(t => t.Namespace != typeof(Command).Namespace).ToList();
            foreach (var commandType in customCommandTypes)
            {
                var metadata = new CommandMetadata {
                    id = commandType.Name,
                    alias = commandType.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(Command.CommandAliasAttribute))?.ConstructorArguments[0].Value as string,
                    localizable = typeof(Command.ILocalizable).IsAssignableFrom(commandType),
                    summary = commandType.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DocumentationAttribute))?.ConstructorArguments[0].Value as string,
                    remarks = commandType.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DocumentationAttribute))?.ConstructorArguments[1].Value as string,
                    @params = ExtractParamsMeta(commandType)
                };
                projectMeta.commands.Add(metadata);
            }

            List<ParameterMetadata> ExtractParamsMeta (Type commandType)
            {
                var result = new List<ParameterMetadata>();
                var fieldInfos = commandType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                    .Where(f => f.FieldType.GetInterface(nameof(ICommandParameter)) != null).ToArray();

                foreach (var fieldInfo in fieldInfos)
                {
                    // Extracting parameter properties.
                    var id = fieldInfo.Name;
                    var alias = fieldInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(Command.ParameterAliasAttribute))?.ConstructorArguments[0].Value as string;
                    var nameless = alias == string.Empty;
                    var required = fieldInfo.CustomAttributes.Any(a => a.AttributeType == typeof(Command.RequiredParameterAttribute));
                    var summary = fieldInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DocumentationAttribute))?.ConstructorArguments[0].Value as string;
                    var resourcePathPrefix = fieldInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(IDEResourceAttribute))?.ConstructorArguments[0].Value as string;
                    var resourcePathPrefixNamedId = resourcePathPrefix != null ? (int)fieldInfo.CustomAttributes.First(a => a.AttributeType == typeof(IDEResourceAttribute)).ConstructorArguments[1].Value : -1;
                    var actorPathPrefix = fieldInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(IDEActorAttribute))?.ConstructorArguments[0].Value as string;
                    var actorPathPrefixNamedId = actorPathPrefix != null ? (int)fieldInfo.CustomAttributes.First(a => a.AttributeType == typeof(IDEActorAttribute)).ConstructorArguments[1].Value : -1;
                    var appearance = fieldInfo.CustomAttributes.Any(a => a.AttributeType == typeof(IDEAppearanceAttribute));
                    var appearanceNamedId = appearance ? (int)fieldInfo.CustomAttributes.First(a => a.AttributeType == typeof(IDEAppearanceAttribute)).ConstructorArguments[0].Value : -1;
                    var constant = fieldInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(IDEConstantAttribute))?.ConstructorArguments[0].Value as string;
                    var constantNamedId = constant != null ? (int)fieldInfo.CustomAttributes.First(a => a.AttributeType == typeof(IDEConstantAttribute)).ConstructorArguments[1].Value : -1;

                    // Extracting parameter value type.
                    string ResolveValueType (Type type)
                    {
                        var valueTypeName = type.GetInterface("INullable`1")?.GetGenericArguments()[0].Name;
                        switch (valueTypeName)
                        {
                            case "String": case "NullableString": return "string";
                            case "Int32": case "NullableInteger": return "int";
                            case "Single": case "NullableFloat": return "float";
                            case "Boolean": case "NullableBoolean": return "bool";
                        }
                        return null;
                    }
                    var dataType = new DataType();
                    var paramType = fieldInfo.FieldType;
                    var isLiteral = ResolveValueType(paramType) != null;
                    if (isLiteral)
                    {
                        dataType.kind = "literal";
                        dataType.contentType = ResolveValueType(paramType);
                    }
                    else if (paramType.GetInterface("IEnumerable") != null)
                    {
                        var elementType = paramType.GetInterface("INullable`1").GetGenericArguments()[0].GetGenericArguments()[0];
                        var namedElementType = elementType.BaseType?.GetGenericArguments()[0];
                        if (namedElementType?.GetInterface("INamedValue") != null) // Treating arrays of named liters as maps for the parser.
                        {
                            dataType.kind = "map";
                            dataType.contentType = ResolveValueType(namedElementType.GetInterface("INamed`1").GetGenericArguments()[0]);
                        }
                        else
                        {
                            dataType.kind = "array";
                            dataType.contentType = ResolveValueType(elementType);
                        }
                    }
                    else
                    {
                        dataType.kind = "namedLiteral";
                        dataType.contentType = ResolveValueType(paramType.GetInterface("INullable`1").GetGenericArguments()[0].GetInterface("INamed`1").GetGenericArguments()[0]);
                    }

                    result.Add(new ParameterMetadata { 
                        id = id,
                        alias = alias,
                        nameless = nameless,
                        required = required,
                        dataType = dataType,
                        summary = summary,
                        resourcePathPrefix = resourcePathPrefix,
                        resourcePathPrefixNamedId = resourcePathPrefixNamedId,
                        actorPathPrefix = actorPathPrefix,
                        actorPathPrefixNamedId = actorPathPrefixNamedId,
                        appearance = appearance,
                        appearanceNamedId = appearanceNamedId,
                        constant = constant,
                        constantNamedId = constantNamedId
                    });
                }

                return result;
            }
        }
    }
}
