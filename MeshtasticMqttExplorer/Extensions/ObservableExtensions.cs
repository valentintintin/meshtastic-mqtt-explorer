using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;

namespace MeshtasticMqttExplorer.Extensions;

public static class ObservableExtensions
{
    /// <summary>
    ///     Allows calling async function but does this in a serial way. Long running tasks will block
    ///     other subscriptions
    /// </summary>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync,
        Action<Exception>? onError = null)
    {
        return source
            .Select(e => Observable.FromAsync(() => HandleTaskSafeAsync(onNextAsync, e, onError, null)))
            .Concat()
            .Subscribe();
    }

    /// <summary>
    ///     Allows calling async function but does this in a serial way. Long running tasks will block
    ///     other subscriptions
    /// </summary>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync,
        ILogger logger)
    {
        return source
            .Select(e => Observable.FromAsync(() => HandleTaskSafeAsync(onNextAsync, e, null, logger)))
            .Concat()
            .Subscribe();
    }

    /// <summary>
    ///     Allows calling async function. Order of messages is not guaranteed
    /// </summary>
    public static IDisposable SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync,
        Action<Exception>? onError = null)
    {
        return source
            .Select(async e => await Observable.FromAsync(() => HandleTaskSafeAsync(onNextAsync, e, onError, null)))
            .Merge()
            .Subscribe();
    }

    /// <summary>
    ///     Allows calling async function with max nr of concurrent messages. Order of messages is not guaranteed
    /// </summary>
    public static IDisposable SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync,
        int maxConcurrent, Action<Exception>? onError = null)
    {
        return source
            .Select(e => Observable.FromAsync(() => HandleTaskSafeAsync(onNextAsync, e, onError, null)))
            .Merge(maxConcurrent)
            .Subscribe();
    }

    /// <summary>
    ///     Allows calling async function with max nr of concurrent messages. Order of messages is not guaranteed
    /// </summary>
    public static IDisposable SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync,
        int maxConcurrent, ILogger? logger = null)
    {
        return source
            .Select(e => Observable.FromAsync(() => HandleTaskSafeAsync(onNextAsync, e, onError:null, logger)))
            .Merge(maxConcurrent)
            .Subscribe();
    }

    /// <summary>
    ///     Subscribe safely where unhandled exception does not unsubscribe and always log error
    /// </summary>
    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, Action<T> onNext,
        Action<Exception>? onError = null)
    {
        return source
            .Select(e => HandleTaskSafe(onNext, e, onError, null))
            .Subscribe();
    }

    /// <summary>
    ///     Subscribe safely where unhandled exception does not unsubscribe and always log error
    ///     to provided ILogger
    /// </summary>
    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, Action<T> onNext,
        ILogger logger)
    {
        return source
            .Select(e => HandleTaskSafe(onNext, e, null, logger))
            .Subscribe();
    }

    [SuppressMessage("", "CA1031")]
    private static T HandleTaskSafe<T>(Action<T> onNext, T e, Action<Exception>? onError = null, ILogger? logger = null)
    {
        try
        {
            onNext(e);
        }
        catch (Exception ex)
        {
            if (onError is not null)
            {
                onError(ex);
            }
            else
            {
                throw;
            }
        }

        return e;
    }

    [SuppressMessage("", "CA1031")]
    private static async Task HandleTaskSafeAsync<T>(Func<T, Task> onNextAsync, T e, Action<Exception>? onError = null, ILogger? logger = null)
    {
        try
        {
            await onNextAsync(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (onError is not null)
            {
                onError(ex);
            }
            else
            {
                throw;
            }
        }
    }
}