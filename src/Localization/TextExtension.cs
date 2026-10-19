using System.Windows;

namespace TanukiTarkovMap.Localization
{
    public sealed class TextExtension : DynamicResourceExtension
    {
        public TextExtension() { }

        public TextExtension(string key) : base(key) { }
    }
}
