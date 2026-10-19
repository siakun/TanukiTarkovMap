using System.IO;
using CefSharp;
using CefSharp.SchemeHandler;
using TanukiTarkovMap.Core.Offline;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Models.Offline;

public static class LocalViewerHost
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "LocalMap");

    public static IRequestContext CreateRequestContext()
    {
        var context = new RequestContext(new RequestContextSettings { CachePath = string.Empty });
        if (Directory.Exists(Root))
            context.RegisterSchemeHandlerFactory("https", LocalViewer.Host, new LocalSchemeHandlerFactory(Root));
        else
            Logger.SimpleLog($"[LocalViewer] Viewer folder missing: {Root}");
        return context;
    }
}

public sealed class LocalSchemeHandlerFactory(string root) : ISchemeHandlerFactory
{
    private readonly ISchemeHandlerFactory _viewer = new FolderSchemeHandlerFactory(root, hostName: LocalViewer.Host);

    public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host != LocalViewer.Host || !uri.IsDefaultPort)
            return NotFound();

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path.StartsWith("/resources/", StringComparison.Ordinal))
        {
            var stream = MapResources.Open(path["/resources/".Length..]);
            if (stream == null) return NotFound();
            return ResourceHandler.FromStream(stream,
                path.EndsWith(".svg", StringComparison.Ordinal) ? "image/svg+xml" : "application/json",
                autoDisposeStream: true, charSet: "utf-8");
        }
        return path.StartsWith("/viewer/", StringComparison.Ordinal)
            ? _viewer.Create(browser, frame, schemeName, request) : NotFound();
    }

    private static IResourceHandler NotFound() => new ResourceHandler { StatusCode = 404, StatusText = "Not Found" };
}
