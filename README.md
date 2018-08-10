The `ArgumentOutOfRangeException` bug (*The UTC time represented when the offset is applied must be between year 0 and 10,000*) in ASP.NET `MemoryCache` internals was probably first seen in the wild back in 2010 or 2011 when .NET Framework 4 was released. It was triggered when the following conditions were met:

* The time zone of the computer the ASP.NET app is hosted must be ahead of UTC.
* A `DateTime.MinValue` value is assigned to a variable of type `DateTimeOffset` somewhere.

This problem was kinda fixed with the hotfix [KB2346777](https://support.microsoft.com/en-us/help/2346777/fix-system-argumentoutofrangeexception-exception-when-you-run-a-net-fr) that was released in 2011. However, since 2013, I am occasionally getting the same error in my ASP.NET apps (.NET 4.6.1 and 4.6.2) on a server with a high traffic but I didn't have time to dig into it until recently.

I was able to reproduce it using the following code in a simple ASP.NET MVC application:

```cs
public class FooController : Controller {
    public String Bar () {
        const Int32 rangeCount = 5000;
        var pathToFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foo.txt"); // Make sure the file exists
        var actions = Enumerable
            .Range(0, rangeCount)
            .Select(i => i % 2 == 0
                ? new Action(() => new HostFileChangeMonitor(new[] { pathToFile }))
                : new Action(() => System.IO.File.SetLastWriteTimeUtc(pathToFile, DateTime.UtcNow))
            )
            .ToArray();

        Parallel.Invoke(actions);

        return "Could not reproduce. Please increase the value of rangeCount and try again!";
    }
}
```

In my humble opinion, the steps that lead to the exception is something like this:

1. A new instance of `HostFileChangeMonitor` is created.
2. The method [`InitDisposableMembers`](https://referencesource.microsoft.com/#System.Runtime.Caching/System/Caching/HostFileChangeMonitor.cs,28) of `HostFileChangeMonitor` is invoked.
3. In our case there's only one file, so `IFileChangeNotificationSystem.StartMonitoring` is invoked [here](https://referencesource.microsoft.com/#System.Runtime.Caching/System/Caching/HostFileChangeMonitor.cs,36).
4. This is an ASP.NET app, so `System.Web.Hosting.ObjectCacheHost` is the implementer.[*](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.caching.hosting.ifilechangenotificationsystem?view=netframework-4.7.1#remarks) So `ObjectCacheHost.StartMonitoring` is invoked.
5. Inside that method, `HttpRuntime.FileChangesMonitor.StartMonitoringPath` is [invoked](https://referencesource.microsoft.com/#System.Web/Hosting/ObjectCacheHost.cs,68). Something goes wrong there and because of that, the variable `fad` is left with a `null` value.
6. [`FileAttributesData.NonExistantAttributesData` is used as a replacement](https://referencesource.microsoft.com/#System.Web/Hosting/ObjectCacheHost.cs,70) and that's probably the problem. Because it uses the default constructor of `FileAttributesData` and it does not assign any value to its `DateTime` fields, leaving them with `default(DateTime)`, which is `DateTime.MinValue`. And `DateTime.MinValue` is unacceptable to `DateTimeOffset` constructor if we're ahead of UTC.

Therefore making sure we set utc values in the default constructor of `FileAttributesData` will fix the problem:

```diff
     FileAttributesData() {
         FileSize = -1;
+        UtcCreationTime = UtcLastAccessTime = UtcLastWriteTime = DateTime.MinValue.ToUniversalTime();
     }
```

By the way, I was unable to reproduce this error in an ASP.NET Core App or a .NET Framework Console App.
