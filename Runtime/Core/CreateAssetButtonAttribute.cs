using System;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Draws a +New button next to ScriptableObject object fields.
    /// The button creates an asset and assigns it to the field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CreateAssetButtonAttribute : PropertyAttribute
    {
        public string FolderPath { get; }
        public string FileNameSuffix { get; }

        public CreateAssetButtonAttribute(string folderPath, string fileNameSuffix = "Asset")
        {
            FolderPath = folderPath;
            FileNameSuffix = fileNameSuffix;
        }
    }
}
