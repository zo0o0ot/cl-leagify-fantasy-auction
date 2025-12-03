# Azure Logic App Setup - Connection Cleanup Scheduler

## Overview
This Azure Logic App calls the connection cleanup endpoint every 15 minutes to automatically clean up idle SignalR connections and enable database auto-pause.

**Cost**: **FREE** (2,880 actions/month < 4,000 free tier)
**Savings**: $50-175/month from database auto-pause

---

## Setup Instructions

### Step 1: Create Logic App (Consumption Plan)

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"Create a resource"** → Search for **"Logic App"**
3. Click **"Create"**

**Configuration:**
- **Subscription**: Your Azure subscription
- **Resource Group**: Same as your Static Web App (or create new)
- **Logic App name**: `leagify-connection-cleanup`
- **Region**: Same region as your Static Web App
- **Plan type**: **Consumption** (pay-per-action)
- **Zone redundancy**: Disabled

4. Click **"Review + Create"** → **"Create"**

---

### Step 2: Design the Workflow

1. Once deployed, click **"Go to resource"**
2. In left menu, click **"Logic app designer"**
3. Choose **"Blank Logic App"** template

#### Add Trigger: Recurrence

1. Search for **"Recurrence"** trigger
2. Click **"Recurrence"**
3. Set parameters:
   - **Interval**: `15`
   - **Frequency**: `Minute`
   - **Time zone**: Your preferred time zone (optional)

**Note**: 15 minutes keeps you in the free tier (2,880 actions/month < 4,000 free). Use 5 minutes for $0.12/month if faster cleanup is needed.

#### Add Action: HTTP

1. Click **"+ New step"**
2. Search for **"HTTP"**
3. Click **"HTTP"** action
4. Set parameters:
   - **Method**: `POST`
   - **URI**: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/system/cleanup-connections`
   - **Headers**: (none needed - endpoint is Anonymous)
   - **Body**: (leave empty)

**Why no authentication?**
The cleanup endpoint uses `AuthorizationLevel.Anonymous` because:
- It's an internal maintenance operation (no sensitive data exposed)
- Calling it just triggers connection cleanup (safe to run anytime)
- Simplifies Logic App configuration (no key management needed)
- The endpoint only returns cleanup statistics (no private information)

That's it! No function keys or authentication headers required.

#### Optional: Add Condition for Logging

1. Click **"+ New step"**
2. Search for **"Condition"**
3. Set condition:
   - **Left**: `Status code` (from HTTP action)
   - **Operator**: `is not equal to`
   - **Right**: `200`

**If true branch:**
1. Add action **"Send an email"** (requires Office 365 or Outlook.com)
2. Configure email alert for failures

---

### Step 3: Save and Enable

1. Click **"Save"** in the toolbar
2. Click **"Enable"** if not already enabled
3. The Logic App will now run every 5 minutes automatically

---

## Verification

### Test Manual Run

1. In Logic App designer, click **"Run Trigger"** → **"Run"**
2. Wait a few seconds
3. Click **"Refresh"** to see run history
4. Click the run to see detailed execution
5. Verify HTTP action shows:
   - Status code: `200`
   - Response body shows cleanup results

### Monitor Runs

1. Go to Logic App **"Overview"** page
2. View **"Runs history"** chart
3. Click any run to see details
4. Check for failures (should be 0)

---

## Expected API Response

```json
{
  "Success": true,
  "CleanedConnections": 2,
  "ZombieConnections": 0,
  "Timestamp": "2025-12-03T03:00:00.000Z"
}
```

---

## Troubleshooting

### Run Failed with 401 Unauthorized
**Problem**: Invalid or missing function key
**Solution**:
1. Get new function key from Static Web App
2. Update HTTP action headers in Logic App

### Run Failed with 404 Not Found
**Problem**: Incorrect URL
**Solution**:
1. Verify Static Web App URL
2. Ensure route is `/api/system/cleanup-connections`
3. Check that latest deployment succeeded

### No Runs Showing
**Problem**: Logic App disabled or trigger not configured
**Solution**:
1. Check Logic App is **Enabled** (Overview page)
2. Verify Recurrence trigger is set to 5 minutes
3. Click "Run Trigger" → "Run" to test immediately

---

## Cost Monitoring

### Track Logic App Costs

1. Go to **"Cost Management + Billing"** in Azure Portal
2. Click **"Cost analysis"**
3. Filter by:
   - **Resource group**: Your resource group
   - **Resource**: `leagify-connection-cleanup`
4. View monthly costs (should be ~$0.12/month)

### Expected Billing

- **First 4,000 actions**: FREE
- **Next 4,640 actions**: $0.116/month
- **Total**: ~$0.12/month

**Breakdown:**
```
12 runs/hour × 24 hours × 30 days = 8,640 runs/month
8,640 - 4,000 (free) = 4,640 paid actions
4,640 × $0.000025 = $0.116
```

---

## Cleanup (If Needed)

To remove the Logic App:
1. Go to Logic App in Azure Portal
2. Click **"Delete"**
3. Type Logic App name to confirm
4. Click **"Delete"**

**Note**: This will stop automatic connection cleanup. Database will not auto-pause and will cost $50-175/month.

---

## Related Documentation

- [AUCTION-CONTROL-TESTING.md](./AUCTION-CONTROL-TESTING.md) - Test 0: Connection cleanup testing
- [Api/Functions/ConnectionCleanupFunction.cs](./Api/Functions/ConnectionCleanupFunction.cs) - HTTP endpoint implementation
- [Azure Logic Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/logic-apps/)
