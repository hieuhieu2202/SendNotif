using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace RemoteControlApi.Services;

public record NotificationStreamEvent(
    string AppKey,
    string AppName,
    int NotificationId,
    string Title,
    string Message,
    DateTime CreatedAt,
    string? Link,
    string? FileUrl,
    NotificationStreamVersion? AppVersion);

public record NotificationStreamVersion(
    int AppVersionId,
    string VersionName,
    string? Platform,
    string? ReleaseNotes,
    string FileUrl,
    string? FileChecksum,
    DateTime ReleaseDate);

public interface INotificationStream
{
    IAsyncEnumerable<NotificationStreamEvent> Subscribe(CancellationToken cancellationToken);

    Task PublishAsync(NotificationStreamEvent @event, CancellationToken cancellationToken = default);
}

public class NotificationStream : INotificationStream
{
    private readonly ConcurrentDictionary<Guid, Channel<NotificationStreamEvent>> _channels = new();

    public IAsyncEnumerable<NotificationStreamEvent> Subscribe(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<NotificationStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var id = Guid.NewGuid();
        _channels[id] = channel;

        cancellationToken.Register(() => CompleteChannel(id));

        return ReadChannel(channel.Reader, id, cancellationToken);
    }

    public async Task PublishAsync(NotificationStreamEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var pair in _channels.ToArray())
        {
            try
            {
                if (!pair.Value.Writer.TryWrite(@event))
                {
                    await pair.Value.Writer.WriteAsync(@event, cancellationToken);
                }
            }
            catch
            {
                CompleteChannel(pair.Key);
            }
        }
    }

    private async IAsyncEnumerable<NotificationStreamEvent> ReadChannel(
        ChannelReader<NotificationStreamEvent> reader,
        Guid id,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            CompleteChannel(id);
        }
    }

    private void CompleteChannel(Guid id)
    {
        if (_channels.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
