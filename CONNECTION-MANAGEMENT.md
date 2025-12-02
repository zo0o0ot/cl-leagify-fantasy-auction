# SignalR Connection Management & Database Auto-Pause

## Problem Statement

SignalR connections were keeping the Azure SQL Database active 24/7, preventing auto-pause and resulting in unexpected monthly costs of $50-175/month. The database should auto-pause within 5-10 minutes of the last connection closing, but lingering connections were blocking this.

## Solution Implemented

### 1. Idle Connection Timeout (10 minutes)
- **ConnectionCleanupFunction.cs**: New Azure Function that identifies and cleans up idle connections
- Runs as HTTP-triggered endpoint (can be called by scheduler)
- Marks users as disconnected if inactive for >10 minutes
- Logs detailed information about cleaned connections

### 2. Zombie Connection Cleanup (30 minutes)
- Identifies "zombie" connections idle for >30 minutes (likely leaked/stuck)
- Provides extra logging for these problematic connections
- Helps identify patterns of connection leakage

### 3. Activity Tracking
- **SignalRFunction.cs**: Added `UpdateActivity` endpoint
- Clients can ping this endpoint to update `LastActiveDate`
- Prevents legitimate active users from being timed out

### 4. Connection Monitoring & Logging
- **GetConnectionStatistics**: Real-time view of all connection stats
- Auction-level breakdown showing which auctions have active connections
- "Can Auto-Pause" indicator showing if database can pause

## API Endpoints

### 1. Cleanup Idle Connections
```
GET/POST /api/system/cleanup-connections
Authorization: Function key (default Azure Functions auth)
```

**Response:**
```json
{
  "Success": true,
  "CleanedConnections": 3,
  "ZombieConnections": 1,
  "Timestamp": "2025-12-01T12:00:00Z"
}
```

**What it does:**
- Finds all users with `IsConnected = true` and `LastActiveDate < now - 10 minutes`
- Marks them as disconnected (sets `IsConnected = false`, `ConnectionId = null`)
- Logs details about each cleaned connection
- Returns count of cleaned connections

### 2. Force Cleanup (Admin)
```
POST /api/admin/cleanup-connections
Headers:
  X-Management-Token: <MANAGEMENT_PASSWORD>
```

**What it does:**
- Same as #1 but requires management authentication
- Useful for manual cleanup during troubleshooting

### 3. Get Connection Statistics (Admin)
```
GET /api/admin/connection-statistics
Headers:
  X-Management-Token: <MANAGEMENT_PASSWORD>
```

**Response:**
```json
{
  "TotalUsers": 25,
  "ConnectedUsers": 3,
  "IdleConnections": 0,
  "ZombieConnections": 0,
  "ActiveAuctions": 2,
  "CanAutoPause": false,
  "IdleTimeoutMinutes": 10.0,
  "ZombieTimeoutMinutes": 30.0,
  "Timestamp": "2025-12-01T12:00:00Z",
  "AuctionBreakdown": [
    {
      "AuctionId": 52,
      "ConnectedUsers": 3,
      "OldestActivity": "2025-12-01T11:55:00Z"
    }
  ]
}
```

**What it shows:**
- Current connection counts
- Whether database can auto-pause (ConnectedUsers == 0)
- Per-auction breakdown of active connections
- Oldest activity timestamp per auction

### 4. Update Activity (Client)
```
POST /api/signalr/update-activity
Content-Type: application/json

{
  "SessionToken": "abc123...",
  "Timestamp": "2025-12-01T12:00:00Z"
}
```

**What it does:**
- Updates user's `LastActiveDate` to prevent timeout
- Should be called by clients periodically (every 5 minutes recommended)

## Deployment Steps

### Step 1: Deploy the Code
```bash
# Build and verify
dotnet build Api/LeagifyFantasyAuction.Api.csproj

# Commit changes
git add .
git commit -m "Add SignalR connection cleanup to enable database auto-pause"
git push
```

Azure Static Web Apps will automatically deploy the new function.

### Step 2: Test the Cleanup Endpoint
```bash
# Get the function URL from Azure Portal or use default
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/system/cleanup-connections"
```

Expected response: JSON with connection cleanup results

### Step 3: Set Up Automated Cleanup (Choose One)

#### Option A: Azure Logic Apps (Recommended)
1. Go to Azure Portal → Create Logic App
2. Choose "Recurrence" trigger
3. Set interval: Every 5 minutes
4. Add "HTTP" action:
   - Method: GET
   - URI: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/system/cleanup-connections`
5. Save and enable

**Cost:** ~$0/month (free tier includes 200,000 actions/month, we only need ~8,640/month)

#### Option B: External Cron Service (cron-job.org, etc.)
1. Sign up for free cron service
2. Create job:
   - URL: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/system/cleanup-connections`
   - Interval: Every 5 minutes
   - Method: GET

#### Option C: Manual Calls During Testing
For now, you can manually call the endpoint periodically until automated scheduler is set up.

### Step 4: Monitor Connection Statistics

Check connection stats via admin endpoint:
```bash
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/connection-statistics" \
  -H "X-Management-Token: <YOUR_MANAGEMENT_PASSWORD>"
```

Look for:
- `CanAutoPause: true` when no users connected
- `IdleConnections: 0` (all active connections are actually active)
- `ZombieConnections: 0` (no leaked connections)

### Step 5: Verify Database Auto-Pause

1. **Wait for connections to clear:**
   - Ensure no users are connected to any auctions
   - Run cleanup endpoint to clear any idle connections
   - Check `CanAutoPause: true` in statistics

2. **Wait 5-10 minutes:**
   - Azure SQL Database auto-pauses after 5 minutes of inactivity
   - Check Azure Portal → SQL Database → Overview
   - Status should change to "Paused"

3. **Monitor for a week:**
   - Check database status daily
   - Should pause every night when no one is connected
   - Cost should drop significantly

## Client-Side Implementation (TODO)

To make this fully effective, client applications should ping the activity endpoint periodically:

```typescript
// In WaitingRoom.razor or other SignalR-connected components

private Timer? _activityTimer;

protected override async Task OnInitializedAsync()
{
    // ... existing initialization ...

    // Start activity ping timer (every 5 minutes)
    _activityTimer = new Timer(async _ => await PingActivity(), null,
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
}

private async Task PingActivity()
{
    try
    {
        var sessionJson = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "auctionSession");
        if (string.IsNullOrEmpty(sessionJson)) return;

        var session = JsonSerializer.Deserialize<UserSession>(sessionJson);
        if (session == null) return;

        await Http.PostAsJsonAsync("/api/signalr/update-activity", new
        {
            SessionToken = session.SessionToken,
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error pinging activity: {ex.Message}");
    }
}

public async ValueTask DisposeAsync()
{
    _activityTimer?.Dispose();
    // ... existing disposal ...
}
```

## Monitoring & Troubleshooting

### Check if cleanup is working:
```bash
# Check logs in Azure Portal → Function App → Log Stream
# Look for messages like:
# "🧹 Starting idle connection cleanup"
# "✅ Cleaned up 3 idle connections (1 zombies)"
```

### If database is NOT auto-pausing:

1. **Check connected users:**
   ```bash
   curl https://your-app.azurestaticapps.net/api/admin/connection-statistics \
     -H "X-Management-Token: <TOKEN>"
   ```

2. **Look for:**
   - `CanAutoPause: false` → Still have connected users
   - `ZombieConnections > 0` → Run cleanup manually
   - Check `AuctionBreakdown` to see which auctions have connections

3. **Force cleanup:**
   ```bash
   curl -X POST https://your-app.azurestaticapps.net/api/admin/cleanup-connections \
     -H "X-Management-Token: <TOKEN>"
   ```

4. **Check database metrics:**
   - Azure Portal → SQL Database → Metrics
   - Look for "Active Connections" metric
   - Should drop to 0 when no users are connected

### Success Indicators:

✅ **Working correctly when:**
- `CanAutoPause: true` when no users are using the app
- Database pauses within 5-10 minutes of last user leaving
- No zombie connections accumulating over time
- Monthly database cost drops to near $0 (only charges for active hours)

❌ **Problem indicators:**
- Database never pauses (always shows "Online" in portal)
- `ZombieConnections` count increasing over time
- `ConnectedUsers` never reaches 0 even when app is unused
- Monthly cost remains at $50-175

## Configuration

Connection timeout thresholds can be adjusted in `ConnectionCleanupFunction.cs`:

```csharp
// Current values:
private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
private static readonly TimeSpan ZombieConnectionTimeout = TimeSpan.FromMinutes(30);

// For more aggressive cleanup, reduce to:
private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
private static readonly TimeSpan ZombieConnectionTimeout = TimeSpan.FromMinutes(15);
```

## Expected Cost Savings

**Before:** Database active 24/7 = ~730 hours/month × $0.07-0.24/hour = **$50-175/month**

**After:** Database active only during usage:
- Development/Testing: ~10-20 hours/month = **$0.70-$4.80/month**
- Production with auctions: ~50-100 hours/month = **$3.50-$24/month**

**Savings:** **$45-170/month** or **$540-2,040/year**

## Next Steps

1. ✅ Deploy code (this PR)
2. ⏳ Set up automated scheduler (Azure Logic Apps recommended)
3. ⏳ Test cleanup manually and verify database pauses
4. ⏳ Add client-side activity pinging (optional, enhances accuracy)
5. ⏳ Monitor for one week to confirm cost savings
6. ✅ Document findings and update DEVELOPMENT-TASKS.md

## Files Changed

- **Api/Functions/ConnectionCleanupFunction.cs** (NEW): Main cleanup logic
- **Api/Functions/SignalRFunction.cs** (MODIFIED): Added UpdateActivity endpoint
- **CONNECTION-MANAGEMENT.md** (NEW): This documentation
