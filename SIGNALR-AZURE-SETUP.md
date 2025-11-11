# Azure SignalR Service Setup Guide

This guide walks you through provisioning and configuring Azure SignalR Service for real-time auction features.

## Overview

The Leagify Fantasy Auction application uses Azure SignalR Service to provide:
- Real-time test bidding in the waiting room
- Live auction bid updates
- Participant connection status monitoring
- Automatic state synchronization across all connected clients

## Prerequisites

- Azure subscription with active credits
- Azure CLI installed (`az --version` to verify)
- Access to Azure Static Web Apps configuration
- Owner or Contributor role on the resource group

## Step 1: Provision Azure SignalR Service

### Option A: Using Azure Portal

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource** → Search for **SignalR Service**
3. Click **Create** and configure:
   - **Resource Group**: Use same group as your Static Web App (e.g., `rg-leagify-auction`)
   - **Name**: `signalr-leagify-auction` (must be globally unique)
   - **Region**: Same as your Static Web App for best performance
   - **Pricing Tier**:
     - **Free**: 20 concurrent connections, 20,000 messages/day (good for development)
     - **Standard**: Scales to thousands of connections (required for production)
   - **Service Mode**: **Serverless** ⚠️ **CRITICAL** - Must be Serverless for Azure Functions
4. Click **Review + create** → **Create**
5. Wait 2-3 minutes for deployment to complete

> **⚠️ Important**: Service Mode must be **Serverless** for Azure Functions. Do NOT use Default mode (that's for persistent ASP.NET Core SignalR servers).

### Option B: Using Azure CLI

```bash
# Set variables
RESOURCE_GROUP="rg-leagify-auction"
SIGNALR_NAME="signalr-leagify-auction"
LOCATION="eastus"  # Use same location as your Static Web App

# Create SignalR Service (Free tier for development)
# IMPORTANT: Must use Serverless mode for Azure Functions
az signalr create \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Free_F1 \
  --service-mode Serverless

# For production, use Standard tier with auto-scaling
# az signalr create \
#   --name $SIGNALR_NAME \
#   --resource-group $RESOURCE_GROUP \
#   --location $LOCATION \
#   --sku Standard_S1 \
#   --unit-count 1 \
#   --service-mode Serverless
```

## Step 2: Get Connection String

### Option A: From Azure Portal

1. Go to your SignalR Service resource in Azure Portal
2. Navigate to **Settings** → **Keys** (left sidebar)
3. Copy the **Primary connection string** (starts with `Endpoint=https://...`)

### Option B: Using Azure CLI

```bash
# Get primary connection string
az signalr key list \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --query primaryConnectionString \
  --output tsv
```

The connection string format looks like:
```
Endpoint=https://signalr-leagify-auction.service.signalr.net;AccessKey=YOUR_ACCESS_KEY;Version=1.0;
```

## Step 3: Configure Static Web App

### Add Connection String to Application Settings

1. Navigate to your **Azure Static Web App** in Azure Portal
2. Go to **Settings** → **Configuration** (left sidebar)
3. Click **+ Add** under "Application settings"
4. Add new setting:
   - **Name**: `AzureSignalRConnectionString`
   - **Value**: Paste the connection string from Step 2
5. Click **OK** → **Save** at the top
6. Static Web App will restart (takes ~30 seconds)

### Using Azure CLI

```bash
STATIC_APP_NAME="your-static-web-app-name"

CONNECTION_STRING=$(az signalr key list \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --query primaryConnectionString \
  --output tsv)

az staticwebapp appsettings set \
  --name $STATIC_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --setting-names AzureSignalRConnectionString="$CONNECTION_STRING"
```

## Step 4: Update Azure Functions for SignalR Bindings

The current SignalRFunction.cs has the routing configured but needs to use proper SignalR bindings.

### Install Required NuGet Package

Verify this package is in `Api/LeagifyFantasyAuction.Api.csproj`:

```xml
<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.SignalRService" Version="1.14.0" />
```

### Update Negotiate Function with SignalR Binding

Replace the current negotiate implementation with proper SignalR input binding:

```csharp
[Function("negotiate")]
public HttpResponseData Negotiate(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/negotiate")] HttpRequestData req,
    [SignalRConnectionInfoInput(HubName = "auctionhub")] string connectionInfo)
{
    _logger.LogInformation("SignalR negotiate endpoint called");

    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "application/json");
    response.WriteString(connectionInfo);
    return response;
}
```

### Create SignalR Output Functions

For broadcasting messages from Azure Functions, use SignalR output bindings:

```csharp
[Function("BroadcastTestBid")]
public async Task<HttpResponseData> BroadcastTestBid(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    [SignalROutput(HubName = "auctionhub")] IAsyncCollector<SignalRMessageAction> signalRMessages)
{
    var broadcastData = await req.ReadFromJsonAsync<TestBidBroadcast>();

    await signalRMessages.AddAsync(new SignalRMessageAction("TestBidPlaced")
    {
        GroupName = $"waiting-{broadcastData.AuctionId}",
        Arguments = new object[]
        {
            broadcastData.BidderName,
            broadcastData.Amount,
            broadcastData.BidDate
        }
    });

    return req.CreateResponse(HttpStatusCode.OK);
}
```

## Step 5: Verify Configuration

### Check Configuration in Azure

```bash
# Verify SignalR service is running
az signalr show \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --query '{name:name,state:provisioningState,endpoint:hostName}' \
  --output table

# Verify Static Web App has connection string configured
az staticwebapp appsettings list \
  --name $STATIC_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "properties.AzureSignalRConnectionString"
```

### Test SignalR Negotiate Endpoint

After deployment, test the negotiate endpoint:

```bash
curl -X POST https://your-static-web-app.azurestaticapps.net/api/signalr/negotiate
```

Expected response with proper configuration:
```json
{
  "url": "https://signalr-leagify-auction.service.signalr.net/client/",
  "accessToken": "eyJ0eXAi...",
  "availableTransports": ["WebSockets", "ServerSentEvents", "LongPolling"]
}
```

## Step 6: Test Real-Time Features

1. Deploy the updated code with SignalR bindings
2. Join an auction with two different browsers/devices
3. Navigate to the waiting room
4. Place a test bid in one browser
5. Verify the bid appears instantly in the other browser without refresh

### Browser Console Verification

Open browser DevTools Console, you should see:
```
🔌 Initializing SignalR connection for auction 53
✅ SignalR connected successfully
✅ Joined waiting room group for auction 53
📢 SignalR: Test bid received - John Doe bid $5
```

## Troubleshooting

### ⚠️ SignalR Connection Fails: "app servers are not connected"

- **Symptom**: `Unable to complete handshake with the server... SignalR Service is now in 'Default' service mode... However app servers are not connected`
- **Cause**: SignalR Service is in wrong service mode (Default instead of Serverless)
- **Fix**:
  1. Azure Portal → SignalR Service → Settings → **Service Mode**
  2. Change from **Default** to **Serverless**
  3. Click Save, wait 30 seconds
  4. Refresh the browser
- **Why**: Azure Functions require Serverless mode. Default mode expects persistent server connections.

### SignalR Connection Fails with 404

- **Symptom**: `POST /api/signalr/negotiate 404 (Not Found)`
- **Cause**: Function routing not configured or deployment incomplete
- **Fix**:
  - Verify `Route = "signalr/negotiate"` is in function attribute
  - Redeploy Azure Functions
  - Check function logs in Azure Portal

### SignalR Connection Fails with 503

- **Symptom**: `503 Service Unavailable: SignalR Service not configured`
- **Cause**: `AzureSignalRConnectionString` not in app settings
- **Fix**: Complete Step 3 to add connection string

### Messages Not Broadcasting

- **Symptom**: Connection succeeds but test bids don't appear in real-time
- **Cause**: SignalR output bindings not implemented
- **Fix**:
  - Update WaitingRoomFunction.PlaceTestBid to call SignalR broadcast function
  - Ensure SignalR output binding is configured (Step 4)

### "InvalidAccessKeyException" in Logs

- **Symptom**: SignalR connects but then disconnects immediately
- **Cause**: Invalid or expired access key in connection string
- **Fix**: Regenerate primary key in Azure Portal → Keys → Regenerate Primary

## Cost Considerations

### Free Tier Limits
- 20 concurrent connections
- 20,000 messages per day
- 2 units maximum

**Good for**: Development and testing with 3-5 users

### Standard Tier Pricing (as of 2024)
- ~$1/day per unit (~$30/month)
- 1,000 concurrent connections per unit
- Unlimited messages
- Auto-scaling available

**Good for**: Production with dozens of simultaneous auctions

### Cost Optimization Tips
- Use Free tier for development/testing
- Scale to Standard only when going to production
- Set auto-scale minimum to 1 unit, maximum to 3 for most use cases
- Monitor usage in Azure Portal → Metrics

## Security Considerations

1. **Connection String Security**
   - Never commit connection strings to source control
   - Use Azure Static Web App configuration (encrypted at rest)
   - Rotate access keys periodically

2. **Service Mode** ⚠️ CRITICAL
   - **MUST use Serverless mode** for Azure Functions
   - Default mode expects persistent ASP.NET Core SignalR servers (won't work with Functions)
   - Classic mode is legacy, not recommended
   - If you see "app servers are not connected" error, mode is set incorrectly

3. **CORS Configuration**
   - SignalR Service auto-configures CORS for Azure Static Web Apps
   - Verify allowed origins in SignalR Service → Settings → CORS

4. **Authentication**
   - Negotiate endpoint validates session tokens
   - Access tokens are short-lived (1 hour default)
   - Users must re-authenticate if connection drops

## Next Steps

Once Azure SignalR Service is configured:

1. ✅ Update SignalRFunction.cs with proper bindings (Step 4)
2. ✅ Implement SignalR broadcasting in WaitingRoomFunction
3. ✅ Test waiting room real-time features
4. ✅ Extend to live auction bidding (Phase 5)
5. ✅ Add reconnection handling for network interruptions

## Resources

- [Azure SignalR Service Documentation](https://learn.microsoft.com/azure/azure-signalr/)
- [SignalR with Azure Functions](https://learn.microsoft.com/azure/azure-signalr/signalr-concept-azure-functions)
- [SignalR Pricing Calculator](https://azure.microsoft.com/pricing/details/signalr-service/)
- [ASP.NET Core SignalR Client](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)
