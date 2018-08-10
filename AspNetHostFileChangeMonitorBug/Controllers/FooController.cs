using System;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace AspNetHostFileChangeMonitorBug.Controllers {
    public class FooController : Controller {
        public String Bar () {
            var now = DateTimeOffset.Now;

            if (now.Offset <= TimeSpan.FromMinutes(0)) {
                return $"Please make sure that your computer's time zone {now:'('K')'} is ahead of UTC.";
            }

            const Int32 rangeCount = 5000;
            var pathToFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foo.txt");
            var actions = Enumerable
                .Range(0, rangeCount)
                .Select(i => i % 2 == 0
                    ? new Action(() => new HostFileChangeMonitor(new[] { pathToFile }))
                    : new Action(() => System.IO.File.SetLastWriteTimeUtc(pathToFile, DateTime.UtcNow))
                )
                .ToArray();

            Parallel.Invoke(actions);

            return "Could not reproduce. Please increase the value of <code>rangeCount</code> and try again!";
        }
    }
}
