using System.Windows;
using System.Windows.Controls.Primitives;

namespace TanukiTarkovMap.Behaviors
{
    public static class CalloutToolTipPlacement
    {
        public static readonly CustomPopupPlacementCallback BelowCenter = (popupSize, targetSize, offset) =>
            new[]
            {
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height),
                    PopupPrimaryAxis.Horizontal)
            };
    }
}
