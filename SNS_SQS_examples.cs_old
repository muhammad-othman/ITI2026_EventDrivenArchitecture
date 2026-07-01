var request = new PublishRequest
{
    TopicArn = _topicArn,
    Message = JsonSerializer.Serialize(orderPlaced),
    MessageAttributes = new Dictionary<string, MessageAttributeValue>
    {
        ["EventType"] = new MessageAttributeValue
        {
            DataType = "String",
            StringValue = "OrderPlaced"   // This is what filters match against
        }
    }
};

public class EventPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly string _topicArn;

    public EventPublisher(IAmazonSimpleNotificationService sns, string topicArn)
    {
        _sns = sns;
        _topicArn = topicArn;
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        // 1. Serialize the event to JSON
        var message = JsonSerializer.Serialize(@event);

        // 2. Build the publish request
        var request = new PublishRequest
        {
            TopicArn = _topicArn,     // Which SNS topic to publish to
            Message = message,         // The actual event payload
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                // 3. Attach the event type as metadata (used for filtering)
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = typeof(T).Name  // "OrderPlaced", "OrderCancelled", etc.
                }
            }
        };

        // 4. Publish — fire and forget, SNS handles fan-out
        await _sns.PublishAsync(request);
    }
}






public class OrderEventsConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;
    private readonly ILogger<OrderEventsConsumer> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Poll the queue — "do you have any messages for me?"
            //    WaitTimeSeconds = 20 means "wait up to 20 seconds for a message"
            //    (long polling — efficient, avoids hammering the queue)
            var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = 10,     // Grab up to 10 at once
                WaitTimeSeconds = 20          // Long polling
            }, stoppingToken);

            foreach (var message in response.Messages)
            {
                try
                {
                    // 2. SNS wraps your original message in an envelope
                    //    We need to unwrap it to get the actual event
                    var snsEnvelope = JsonSerializer.Deserialize<SnsEnvelope>(message.Body);
                    var orderPlaced = JsonSerializer.Deserialize<OrderPlaced>(snsEnvelope!.Message);

                    // 3. Do your business logic
                    _logger.LogInformation("Restaurant received order {OrderId}", orderPlaced!.OrderId);
                    await ProcessOrder(orderPlaced);

                    // 4. Delete the message — "I'm done with this, don't send it again"
                    //    If we crash before this line, the message reappears (at-least-once!)
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message");
                    // Don't delete — message will reappear after visibility timeout
                    // After N failures, it moves to the DLQ
                }
            }
        }
    }
}

// The SNS envelope structure — SNS wraps your message in this
public record SnsEnvelope(string Message, string MessageId, string Type);