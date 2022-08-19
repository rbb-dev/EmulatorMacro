﻿using System.IO;

namespace Utils
{
    public class ConstHelper
    {
        public static readonly string DefaultSavePath = @".\Save\";
        public static readonly string DefaultSaveFileName = "save.dll";
        public static readonly string DefaultConfigFile = "config.json";
        public static readonly string DefaultDatasFilePath = @".\Datas\";
        public static readonly string DefaultSaveCacheFile = @"Cache.dll";
        public static readonly string DefaultPatcherName = @"Patcher.exe";

        public static readonly string TempPath = Path.GetTempPath() + $"Macro\\";
        public static readonly string TempBackupPath = $"{ TempPath }backup\\";

        public static readonly int MinPeriod = 100;
        public static readonly int MinItemDelay = 100;
        public static readonly int MinSimilarity = 0;
        public static readonly int MinDragDelay = 1;

        public static readonly int DefaultSimilarity = 70;
        public static readonly float DefaultDPI = 96.0F;
        public static readonly short WheelDelta = 120;
        public static readonly int DefaultMaxSameImageCount = 5;

        
        public static readonly string VersionUrl = @"http://drive.google.com/uc?export=view&id=188XWJsrFW39ty-VltB-7OjdYZf9FV3Q1";
        public static readonly string ReleaseUrl = @"https://github.com/EomTaeWook/EmulatorMacro/releases";
        
        public static readonly string HelpUrl = @"https://github.com/EomTaeWook/EmulatorMacro/blob/master/README.md";
    }
}
