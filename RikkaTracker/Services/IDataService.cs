using System.Collections.Generic;
using System.Threading.Tasks;
using RikkaTracker.Models;

namespace RikkaTracker.Services
{
    public interface IDataService
    {
        Task SaveAppUsageAsync(IEnumerable<AppUsage> usage);
        Task<IEnumerable<AppUsage>> LoadAppUsageAsync();
        
        Task SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage);
        Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync();
    }
}
