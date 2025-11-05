# SignalR Setup Guide for Azure Static Web Apps

## Current Implementation Status

### ✅ Completed
- **Client-Side SignalR**: Blazor WASM has SignalR Client package installed
- **Waiting Room Component**: WaitingRoom.razor has full SignalR connection logic
- **Hub Definition**: AuctionHub.cs defines all necessary methods for waiting room
- **API Packages**: SignalR Service extensions installed in API project
- **Event Handlers**: Client listens for TestBidPlaced, ReadinessUpdated, UserJoined events

### 🚧 Requires Azure Configuration
- **Azure SignalR Service**: Needs to be provisioned and configured
- **Connection String**: Environment variable `AzureSignalRConnectionString` must be set
- **Negotiate Endpoint**: Needs SignalR output binding configuration
- **Broadcasting**: WaitingRoomFunction needs to send messages to SignalR groups

## Azure Setup Steps

### 1. Create Azure SignalR Service
```bash
# Create SignalR Service instance
az signalr create \
  --name leagify-signalr \
  --resource-group leagify-rg \
  --sku Standard_S1 \
  --service-mode Default \
  --location eastus
```

### 2. Get Connection String
```bash
# Get primary connection string
az signalr key list \
  --name leagify-signalr \
  --resource-group leagify-rg \
  --query primaryConnectionString \
  --output tsv
```

### 3. Configure Static Web App
Add the connection string to Static Web App configuration:

**Azure Portal:**
1. Navigate to Static Web App → Configuration
2. Add Application Setting:
   - Name: `AzureSignalRConnectionString`
   - Value: `<connection string from step 2>`

**Azure CLI:**
```bash
az staticwebapp appsettings set \
  --name leagify-auction \
  --setting-names AzureSignalRConnectionString="<connection-string>"
```

### 4. Update SignalR Function with Output Bindings

The `SignalRFunction.cs` negotiate endpoint needs to use proper SignalR bindings:

```csharp
[Function("negotiate")]
[SignalRConnectionInfoInput(HubName = "AuctionHub", ConnectionStringSetting = "AzureSignalRConnectionString")]
public HttpResponseData Negotiate(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/negotiate")] HttpRequestData req,
    [SignalRConnectionInfoInput(HubName = "AuctionHub", ConnectionStringSetting = "AzureSignalRConnectionString")] string connectionInfo)
{
    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "application/json");
    response.WriteString(connectionInfo);
    return response;
}
```

### 5. Add Broadcasting to WaitingRoomFunction

Update `PlaceTestBid` to broadcast via SignalR output binding:

```csharp
[Function("PlaceTestBid")]
[SignalROutput(HubName = "AuctionHub", ConnectionStringSetting = "AzureSignalRConnectionString")]
public async Task<SignalRMessageAction> PlaceTestBid(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/test-bid")] HttpRequestData req,
    int auctionId)
{
    // ... existing logic ...

    // Return SignalR message to broadcast
    return new SignalRMessageAction("TestBidPlaced")
    {
        GroupName = $"waiting-{auctionId}",
        Arguments = new object[] { user.DisplayName, bidRequest.Amount, DateTime.UtcNow }
    };
}
```

## Testing SignalR

### Local Testing
SignalR Service cannot be fully tested locally. For development:
1. Use Azure SignalR Service emulator (if available)
2. Deploy to Azure Static Web Apps for real testing
3. Monitor browser console for SignalR connection logs

### Multi-User Testing on Azure
1. Deploy application to Azure Static Web Apps
2. Open auction in multiple browser windows/tabs
3. Place test bids and verify real-time updates across all windows
4. Check browser console for SignalR messages:
   - `🔌 Initializing SignalR connection...`
   - `✅ SignalR connected successfully`
   - `✅ Joined waiting room group for auction X`
   - `📢 SignalR: Test bid received...`

## Current Fallback Behavior

Without Azure SignalR Service configured:
- ✅ Application works but without real-time updates
- ✅ Users can place test bids (saved to database)
- ✅ Users can update readiness status
- ⚠️ Users must manually refresh to see others' actions
- ⚠️ SignalR connection will fail gracefully (logged to console)

## SignalR Events Reference

### Waiting Room Events
| Event Name | Parameters | Purpose |
|------------|-----------|---------|
| `TestBidPlaced` | bidderName, amount, bidDate | Broadcast test bid to all participants |
| `ReadinessUpdated` | displayName, isReady | User toggled ready status |
| `UserJoinedWaitingRoom` | displayName | New user joined waiting room |
| `UserTestedBidding` | displayName | User completed first test bid |

### Hub Methods
| Method | Parameters | Purpose |
|--------|-----------|---------|
| `JoinWaitingRoom` | auctionId, displayName | Join waiting room SignalR group |
| `BroadcastTestBid` | auctionId, bidderName, amount | Server-side broadcast helper |
| `BroadcastReadinessUpdate` | auctionId, displayName, isReady | Server-side readiness broadcast |

## Next Steps After SignalR Configuration

1. Test multi-user waiting room functionality
2. Implement Auction Master dashboard with real-time participant monitoring
3. Extend SignalR to live auction bidding (Phase 5)
4. Add connection status monitoring and reconnection handling

## Resources

- [Azure SignalR Service Documentation](https://docs.microsoft.com/azure/azure-signalr/)
- [SignalR with Azure Functions](https://docs.microsoft.com/azure/azure-signalr/signalr-concept-azure-functions)
- [Azure Static Web Apps with Azure Functions](https://docs.microsoft.com/azure/static-web-apps/apis)
