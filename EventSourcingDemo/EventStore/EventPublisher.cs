using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Text.Json;

namespace EventSourcingDemo.EventStore;

/// <summary>
/// Publishes integration events to SNS for consumption by other services.
/// This is the bridge between the internal Event Store (domain events)
/// and the external messaging system (integration events).
/// </summary>
public class SnsEventPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly string _topicArn;
    private readonly ILogger<SnsEventPublisher> _logger;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService sns,
        string topicArn,
        ILogger<SnsEventPublisher> logger)
    {
        _sns = sns;
        _topicArn = topicArn;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        var message = JsonSerializer.Serialize(@event);

        var request = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = message,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = typeof(T).Name
                }
            }
        };

        await _sns.PublishAsync(request);
        _logger.LogInformation("[SNS] Published {EventType}", typeof(T).Name);
    }
}

/// <summary>
/// A no-op publisher for when SNS is not configured (e.g., during demos).
/// Logs the event instead of publishing.
/// </summary>
public class ConsoleEventPublisher
{
    private readonly ILogger<ConsoleEventPublisher> _logger;

    public ConsoleEventPublisher(ILogger<ConsoleEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T @event) where T : class
    {
        var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation(
            "[Integration Event Published] {EventType}:\n{Json}",
            typeof(T).Name, json);
        return Task.CompletedTask;
    }
}
