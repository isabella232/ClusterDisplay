﻿using System;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct DirectorySyncInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.PathMaxLength)]
        public readonly string RemoteDirectory;

        public DirectorySyncInfo(string remoteDirectory)
        {
            RemoteDirectory = remoteDirectory;
        }
    }
}