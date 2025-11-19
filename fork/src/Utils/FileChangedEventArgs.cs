using System;

namespace TarkovClient
{
    public class FileChangedEventArgs : EventArgs
    {
        public string FullPath { get; }

        public FileChangedEventArgs(string fullPath)
        {
            FullPath = fullPath;
        }
    }
}
