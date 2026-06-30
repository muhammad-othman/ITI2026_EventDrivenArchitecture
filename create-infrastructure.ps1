# ============================================
# Step 2: Create SNS Topic and SQS Queues
# ============================================
# Run this ONCE per team. Only ONE team member runs this.
# Share the output with your entire team.

param(
    [Parameter(Mandatory=$true)]
    [string]$TeamName
)

$region = "us-east-1"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Creating infrastructure for: $TeamName" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# --- Create SNS Topic ---
Write-Host "Creating SNS topic..." -ForegroundColor Yellow
$topicArn = (aws sns create-topic --name "$TeamName-order-events" --region $region --query "TopicArn" --output text)
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to create SNS topic!" -ForegroundColor Red; exit 1 }
Write-Host "  Topic ARN: $topicArn" -ForegroundColor Green

# --- Create SQS Queues ---
Write-Host ""
Write-Host "Creating SQS queues..." -ForegroundColor Yellow

$restaurantQueueUrl = (aws sqs create-queue --queue-name "$TeamName-restaurant-queue" --region $region --query "QueueUrl" --output text)
Write-Host "  Restaurant queue: $restaurantQueueUrl" -ForegroundColor Green

$notificationQueueUrl = (aws sqs create-queue --queue-name "$TeamName-notification-queue" --region $region --query "QueueUrl" --output text)
Write-Host "  Notification queue: $notificationQueueUrl" -ForegroundColor Green

$deliveryQueueUrl = (aws sqs create-queue --queue-name "$TeamName-delivery-queue" --region $region --query "QueueUrl" --output text)
Write-Host "  Delivery queue: $deliveryQueueUrl" -ForegroundColor Green

# --- Get Queue ARNs ---
Write-Host ""
Write-Host "Getting queue ARNs..." -ForegroundColor Yellow

$restaurantQueueArn = (aws sqs get-queue-attributes --queue-url $restaurantQueueUrl --attribute-names QueueArn --region $region --query "Attributes.QueueArn" --output text)
$notificationQueueArn = (aws sqs get-queue-attributes --queue-url $notificationQueueUrl --attribute-names QueueArn --region $region --query "Attributes.QueueArn" --output text)
$deliveryQueueArn = (aws sqs get-queue-attributes --queue-url $deliveryQueueUrl --attribute-names QueueArn --region $region --query "Attributes.QueueArn" --output text)

# --- Allow SNS to send to each SQS queue ---
Write-Host "Setting queue policies (allow SNS to deliver messages)..." -ForegroundColor Yellow

foreach ($queueInfo in @(
    @{Url=$restaurantQueueUrl; Arn=$restaurantQueueArn; Name="Restaurant"},
    @{Url=$notificationQueueUrl; Arn=$notificationQueueArn; Name="Notification"},
    @{Url=$deliveryQueueUrl; Arn=$deliveryQueueArn; Name="Delivery"}
)) {
    $policy = @"
{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"sns.amazonaws.com"},"Action":"sqs:SendMessage","Resource":"$($queueInfo.Arn)","Condition":{"ArnEquals":{"aws:SourceArn":"$topicArn"}}}]}
"@
    aws sqs set-queue-attributes --queue-url $queueInfo.Url --attributes "{""Policy"":""$($policy -replace '"','\"')""}" --region $region 2>$null
    if ($LASTEXITCODE -ne 0) {
        # Try alternative approach
        $tempFile = [System.IO.Path]::GetTempFileName()
        @{Policy=$policy} | ConvertTo-Json | Set-Content $tempFile
        aws sqs set-queue-attributes --queue-url $queueInfo.Url --attributes file://$tempFile --region $region 2>$null
        Remove-Item $tempFile
    }
    Write-Host "  $($queueInfo.Name) queue policy set." -ForegroundColor Green
}

# --- Subscribe Queues to SNS Topic ---
Write-Host ""
Write-Host "Subscribing queues to SNS topic..." -ForegroundColor Yellow

aws sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $restaurantQueueArn --region $region --output text > $null
Write-Host "  Restaurant queue subscribed." -ForegroundColor Green

aws sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $notificationQueueArn --region $region --output text > $null
Write-Host "  Notification queue subscribed." -ForegroundColor Green

aws sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $deliveryQueueArn --region $region --output text > $null
Write-Host "  Delivery queue subscribed." -ForegroundColor Green

# --- Summary ---
Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  SETUP COMPLETE!" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "COPY EVERYTHING BELOW AND SHARE WITH YOUR TEAM:" -ForegroundColor White
Write-Host "-------------------------------------------------" -ForegroundColor White
Write-Host ""
Write-Host "SNS_TOPIC_ARN=$topicArn"
Write-Host "RESTAURANT_QUEUE_URL=$restaurantQueueUrl"
Write-Host "NOTIFICATION_QUEUE_URL=$notificationQueueUrl"
Write-Host "DELIVERY_QUEUE_URL=$deliveryQueueUrl"
Write-Host ""
Write-Host "-------------------------------------------------" -ForegroundColor White
Write-Host ""
Write-Host "Each student:" -ForegroundColor Yellow
Write-Host "  1. Paste the SNS_TOPIC_ARN into your Program.cs" -ForegroundColor White
Write-Host "  2. Paste YOUR queue URL into your Consumer class" -ForegroundColor White
Write-Host "  3. Run: dotnet run" -ForegroundColor White
Write-Host ""
