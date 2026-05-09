using System.Windows.Media;

namespace RikkaTracker.Services
{
    public interface IIconService
    {
        ImageSource? GetIcon(string processName, string processPath);
    }
}
