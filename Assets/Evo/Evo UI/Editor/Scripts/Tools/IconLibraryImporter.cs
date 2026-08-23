using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Evo.UI
{
    public class IconLibraryImporter : EditorWindow
    {
        IconLibrary targetLibrary;
        string rootPath = "Assets/Evo/Evo UI/Sprites/Icons";
        bool clearExisting;
        Vector2 scrollPos;

        public static void ShowWindow()
        {
            var window = GetWindow<IconLibraryImporter>("Icon Library Importer");
            window.minSize = new Vector2(300, 250);
            window.Show();
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Target Library
            targetLibrary = (IconLibrary)EditorGUILayout.ObjectField("Target Icon Library", targetLibrary, typeof(IconLibrary), false);

            EditorGUILayout.Space(2);

            // Root Path
            EditorGUILayout.BeginHorizontal();
            rootPath = EditorGUILayout.TextField("Root Folder Path", rootPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Root Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Convert absolute path to relative Unity path
                    if (path.StartsWith(Application.dataPath))
                    {
                        rootPath = "Assets" + path[Application.dataPath.Length..];
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Options
            clearExisting = EditorGUILayout.Toggle("Clear Existing Icons", clearExisting);

            EditorGUILayout.Space(4);

            // Import Button
            GUI.enabled = targetLibrary != null && !string.IsNullOrEmpty(rootPath);
            if (GUILayout.Button("Import Icons", GUILayout.Height(30))) { ImportIcons(); }
            GUI.enabled = true;

            EditorGUILayout.Space(4);

            // Info Box
            EditorGUILayout.HelpBox(
                "How it works:\n" +
                "• Scans the root folder and all subfolders\n" +
                "• Uses folder name as Category (e.g., 'Icons/Media/' → Category: 'Media')\n" +
                "• Uses texture name as Icon ID (e.g., 'Play Circle.png' → ID: 'Play Circle')\n" +
                "• Automatically detects sprite resolution from texture import settings\n" +
                "• Only processes Sprite assets",
                MessageType.Info
            );

            EditorGUILayout.EndScrollView();
        }

        void ImportIcons()
        {
            if (targetLibrary == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target Icon Library.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(rootPath))
            {
                EditorUtility.DisplayDialog("Error", $"The path '{rootPath}' is not a valid folder.", "OK");
                return;
            }

            // Clear existing if requested
            if (clearExisting)
            {
                targetLibrary.icons.Clear();
                targetLibrary.categories.Clear();
            }

            // Find all sprites in the root path and subdirectories
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { rootPath });

            int importedCount = 0;
            int skippedCount = 0;
            HashSet<string> processedCategories = new(targetLibrary.categories);

            EditorUtility.DisplayProgressBar("Importing Icons", "Processing sprites...", 0);

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

                if (sprite == null)
                    continue;

                // Get the directory path relative to root
                string directoryPath = Path.GetDirectoryName(assetPath);
                string category = GetCategoryFromPath(directoryPath, rootPath);

                // Get icon ID from sprite name
                string iconID = sprite.name;

                // Get texture resolution - use the actual texture size
                int resolution = Mathf.Max(sprite.texture.width, sprite.texture.height);

                // Check if icon already exists
                var existingItem = targetLibrary.icons.FirstOrDefault(item => item != null && item.ID == iconID);

                if (existingItem != null)
                {
                    // Icon exists - check if this resolution already exists
                    bool resolutionExists = existingItem.resolutions != null &&
                        existingItem.resolutions.Any(r => r != null && r.resolution == resolution);

                    if (resolutionExists)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Add new resolution to existing item
                    existingItem.resolutions ??= new List<IconLibrary.Item.Resolution>();
                    existingItem.resolutions.Add(new IconLibrary.Item.Resolution
                    {
                        resolution = resolution,
                        sprite = sprite
                    });

                    importedCount++;
                }
                else
                {
                    // Create new icon item
                    var newItem = new IconLibrary.Item
                    {
                        ID = iconID,
                        category = category,
                        resolutions = new List<IconLibrary.Item.Resolution>
                        {
                            new()
                            {
                                resolution = resolution,
                                sprite = sprite
                            }
                        }
                    };

                    targetLibrary.icons.Add(newItem);

                    // Add category if new
                    if (!string.IsNullOrEmpty(category) && !processedCategories.Contains(category))
                    {
                        targetLibrary.categories.Add(category);
                        processedCategories.Add(category);
                    }

                    importedCount++;
                }

                EditorUtility.DisplayProgressBar("Importing Icons", $"Imported: {importedCount}", (float)i / guids.Length);
            }

            EditorUtility.ClearProgressBar();

            // Mark as dirty and save
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssets();
            targetLibrary.InvalidateCache();

            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Successfully imported {importedCount} icons/resolutions.\nSkipped {skippedCount} duplicate resolutions.",
                "OK"
            );
        }

        string GetCategoryFromPath(string fullPath, string rootPath)
        {
            // Normalize paths
            fullPath = fullPath.Replace("\\", "/");
            rootPath = rootPath.Replace("\\", "/");

            // Remove root path from full path
            if (fullPath.StartsWith(rootPath)) { fullPath = fullPath[rootPath.Length..].TrimStart('/'); }

            // Get the last folder name as category
            if (string.IsNullOrEmpty(fullPath)) { return ""; }

            string[] folders = fullPath.Split('/');
            return folders.Length > 0 ? folders[^1] : "";
        }
    }
}