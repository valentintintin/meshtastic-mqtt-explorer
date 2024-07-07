using System.Diagnostics;
using System.Globalization;
using AntDesign;

namespace MeshtasticMqttExplorer;

public class PerformanceAndCultureMiddleware(
    RequestDelegate requestDelegate,
    ILogger<PerformanceAndCultureMiddleware> logger)
{
    public Task Invoke(HttpContext httpContext)
    {
        var headersAcceptLanguage = httpContext.Request.Headers.AcceptLanguage;
        var firstLanguage = headersAcceptLanguage.FirstOrDefault()?.Split(',').FirstOrDefault();
        var culture = CultureInfo.GetCultureInfo(firstLanguage ?? "fr");
        
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        LocaleProvider.SetLocale(culture.Name);	
        
        Stopwatch watch = new();
        watch.Start();

        var nextTask = requestDelegate.Invoke(httpContext);
        nextTask.ContinueWith(t =>
        {
            var time = watch.ElapsedMilliseconds;
            var requestString = $"[{httpContext.Request.Method}]{httpContext.Request.Path}{httpContext.Request.QueryString}";
            if (t.Status == TaskStatus.RanToCompletion)
            {
                logger.LogInformation("{time}ms {requestString}", time, requestString);
            }
            else
            {
                logger.LogWarning(t.Exception?.InnerException, "{time}ms [{status}] - {requestString}", time, t.Status, requestString);
            }
        });
        return nextTask;
    }
}