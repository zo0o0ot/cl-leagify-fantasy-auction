# System Admin Guide

## Table of Contents
1. [Overview](#overview)
2. [Authentication & Access](#authentication--access)
3. [Managing Multiple Auctions](#managing-multiple-auctions)
4. [Database Management](#database-management)
5. [Connection Management](#connection-management)
6. [Troubleshooting](#troubleshooting)
7. [Security & Maintenance](#security--maintenance)
8. [Monitoring & Logs](#monitoring--logs)
9. [Quick Reference](#quick-reference)

---

## Overview

### What is System Admin?
The System Admin interface provides centralized management for all auctions, users, and system-level operations. Unlike the Auction Master role (which manages a single auction), System Admin has access to:
- All auctions across the entire system
- Database connection statistics
- SignalR connection cleanup
- System health monitoring
- Bulk operations (archive, delete test data)

### Who Needs This Guide?
- **System Administrators**: Managing the production environment
- **Technical Support**: Troubleshooting user issues
- **DevOps**: Monitoring database costs and performance

### Architecture Overview
```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Static Web Apps                     │
│  ┌──────────────────────┐  ┌───────────────────────────┐   │
│  │  Blazor WASM Client  │  │  Azure Functions (API)    │   │
│  │  - User Interface    │  │  - Auction Management     │   │
│  │  - SignalR Client    │  │  - Bid Processing         │   │
│  └──────────────────────┘  │  - Connection Cleanup     │   │
│                             └───────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                    │                      │
         ┌──────────┴──────────┐  ┌────────┴─────────┐
         │  Azure SignalR      │  │  Azure SQL       │
         │  Service            │  │  Database (Free) │
         │  - Real-time sync   │  │  - Auto-pause    │
         └─────────────────────┘  └──────────────────┘
```

---

## Authentication & Access

### System Admin Password
Access to system admin requires a management password.

**Security Format:**
- Password format: `admin:YYYY-MM-DDTHH:MM:SSZ` (base64-encoded on backend)
- Example: `admin:2025-12-02T10:30:00Z` → base64 encoded
- Stored in environment variable: `MANAGEMENT_PASSWORD`

**Accessing System Admin:**
1. Navigate to: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/management/system-admin`
2. Enter your management password
3. Password is stored in browser's localStorage for convenience
4. Re-enter if you clear browser data or switch browsers

**Security Best Practices:**
- ⚠️ Never share management password publicly
- ⚠️ Never commit password to git repository
- ✅ Store password in secure password manager
- ✅ Rotate password periodically (update environment variable in Azure)
- ✅ Use HTTPS only (already enforced by Azure Static Web Apps)

### Master Recovery Codes
Each auction has a unique 16-character Master Recovery Code.

**Purpose:**
- Backup access to specific auction if Auction Master loses access
- Can be used to regain control of auction without system admin password

**When to Use:**
- Auction Master lost their browser session
- Original creator unavailable but auction must continue
- Emergency access needed during live auction

**How to Use:**
1. Navigate to join page: `/join`
2. Enter Master Recovery Code instead of join code
3. System grants Auction Master role automatically
4. Access WaitingRoomAdmin for that specific auction

**Important:** Master Recovery Codes are auction-specific, not system-wide.

### System Admin Access to Auction Admin Panels

**Feature:** System admins can access the WaitingRoomAdmin panel for ANY auction, not just auctions they created.

**Use Cases:**
- You're the system admin AND running a specific auction as Auction Master
- Need to troubleshoot issues in someone else's auction
- Taking over auction management temporarily

**How to Enable:**
1. Log in to System Admin with management password
2. In System Admin settings, enable **"Use Management As Admin"**
3. This setting persists in your browser

**How It Works:**
- When enabled, you can access `/management/auction/{auctionId}/waiting-room-admin` for any auction
- You have full Auction Master controls (pause, resume, end, etc.)
- Your access is based on system admin credentials, not auction-specific role

**Important Distinctions:**

| Aspect | Regular Auction Master | System Admin as Auction Master |
|--------|------------------------|--------------------------------|
| **Access Scope** | Single auction only | All auctions system-wide |
| **How Granted** | Created auction OR assigned role | System admin setting enabled |
| **Authentication** | Join code or Master Recovery Code | Management password |
| **Visible to Participants** | Yes, listed as Auction Master | Yes, functions identically |
| **Can Lose Access** | Yes, if browser session lost | No, re-login with admin password |

**Best Practice:**
- If you're running YOUR OWN auction: Join as regular participant first, then use system admin access as backup
- If you're troubleshooting SOMEONE ELSE'S auction: Use system admin access directly
- Document when you use system admin access for audit purposes

**Security Note:** Only grant system admin password to trusted individuals, as it provides access to ALL auctions.

---

## Managing Multiple Auctions

### Viewing All Auctions
From System Admin dashboard:
1. Click **"View All Auctions"**
2. Table displays:
   - Auction ID
   - Auction Name
   - Status (Draft, InProgress, Paused, Complete, Archived)
   - Number of participants
   - Created date
   - Last activity timestamp

**Sorting & Filtering:**
- Click column headers to sort
- Use status filter dropdown to show only specific states
- Search by auction name or ID

### Auction Lifecycle States

| Status | Description | Can Transition To |
|--------|-------------|-------------------|
| **Draft** | Auction created, participants joining, not started | InProgress, Archived |
| **InProgress** | Auction actively running, bidding enabled | Paused, Complete, Archived |
| **Paused** | Temporarily stopped, state preserved | InProgress, Complete, Archived |
| **Complete** | Auction finished, results available | Archived |
| **Archived** | Historical record, no further changes | (None - terminal state) |

### Creating Auctions (Via System Admin)
1. Click **"Create New Auction"**
2. Enter details:
   - **Auction Name**: Descriptive identifier
   - **Number of Teams**: Default 6 (most common)
   - **Budget Per Team**: Default $200
3. System generates:
   - 6-character Join Code (e.g., `A7X3K9`)
   - 16-character Master Recovery Code
4. **Important:** Copy both codes immediately and store securely

### Archiving Auctions
**When to Archive:**
- Auction completed and results exported
- No longer need active access (reduces clutter)
- Want to preserve historical data without deletion

**How to Archive:**
1. Select auction(s) from list
2. Click **"Archive Selected"**
3. Confirm action
4. Archived auctions move to separate "Archived" tab

**What Happens:**
- ✅ Data preserved in database
- ✅ Still viewable in read-only mode
- ✅ Can export results later
- ❌ Cannot be resumed or modified
- ❌ Cannot transition to other states

### Deleting Test Auctions
**Warning:** Deletion is permanent and cannot be undone.

**When to Delete:**
- Test auctions no longer needed
- Development/staging data cleanup
- Freeing up join codes

**How to Delete:**
1. Select test auction(s)
2. Click **"Delete Selected"**
3. Confirm deletion (requires typing "DELETE" to confirm)
4. All related data deleted:
   - Auction record
   - All participant records
   - All bid history
   - All roster assignments
   - All team data

**What NOT to Delete:**
- Production auctions (archive instead)
- Auctions with real money or prizes involved
- Historical auctions you might need to reference

---

## Database Management

### Azure SQL Database (Free Tier)
Current configuration uses Azure SQL Database free tier with auto-pause capability.

**Free Tier Limits:**
- **Storage**: 32 GB maximum
- **Compute**: 5 vCore seconds per hour
- **Auto-pause**: After 5-10 minutes of inactivity
- **Cost**: ~$0/month when paused, charged only for active hours

**Why Auto-Pause Matters:**
- Without cleanup: Database stays active 24/7 = $50-175/month
- With connection cleanup: Database pauses when unused = $0.70-$24/month
- **Savings: $45-170/month** or **$540-2,040/year**

### Connection Cleanup System
See [CONNECTION-MANAGEMENT.md](CONNECTION-MANAGEMENT.md) for detailed documentation.

**How It Works:**
1. **Idle Timeout (10 minutes)**: Users with no activity for 10+ minutes marked as disconnected
2. **Zombie Timeout (30 minutes)**: Leaked connections older than 30 minutes cleaned up
3. **Automated Cleanup**: Scheduled job runs every 5 minutes to clean idle connections
4. **Database Auto-Pause**: When all connections closed, database pauses after 5 minutes

**Monitoring Connection Health:**
```bash
# Check connection statistics
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/connection-statistics" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

**Response:**
```json
{
  "TotalUsers": 25,
  "ConnectedUsers": 3,
  "IdleConnections": 0,
  "ZombieConnections": 0,
  "CanAutoPause": false,
  "AuctionBreakdown": [
    {
      "AuctionId": 52,
      "ConnectedUsers": 3,
      "OldestActivity": "2025-12-02T11:55:00Z"
    }
  ]
}
```

**Key Indicators:**
- ✅ **CanAutoPause: true** - Database will pause soon (no active connections)
- ⚠️ **ZombieConnections > 0** - Leaked connections need cleanup
- ⚠️ **IdleConnections > 0** - Users inactive but still marked connected

**Manual Cleanup:**
```bash
# Force cleanup of idle connections
curl -X POST "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/cleanup-connections" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

### Database Status Verification
**Check if Database is Paused:**
1. Go to Azure Portal
2. Navigate to: SQL databases → `leagify-auction-db`
3. Overview tab shows status:
   - **Paused** (green) - Not consuming resources, $0/hour
   - **Online** (blue) - Active, being charged per hour

**Expected Behavior:**
- Database should pause every night when no auctions running
- Should wake up automatically when first user connects (takes 10-30 seconds)
- If database never pauses, check connection statistics for zombie connections

---

## Connection Management

### Understanding SignalR Connections
SignalR provides real-time updates for auction bidding. Each connected user maintains an active database connection.

**Connection Lifecycle:**
1. User joins auction → SignalR connection established
2. User places bids → Connection sends updates to all participants
3. User closes browser → Connection should disconnect automatically
4. **Problem:** Some browsers don't always close connections properly (zombies)

### Connection Cleanup Endpoints

#### 1. Get Connection Statistics
**Purpose:** Monitor current connection health

**Endpoint:**
```
GET /api/admin/connection-statistics
Authorization: X-Management-Token header
```

**When to Use:**
- Checking why database won't auto-pause
- Investigating zombie connections
- Monitoring active auction participants

#### 2. Manual Connection Cleanup
**Purpose:** Force cleanup of idle connections

**Endpoint:**
```
POST /api/admin/cleanup-connections
Authorization: X-Management-Token header
```

**When to Use:**
- Database not auto-pausing despite no active users
- Many zombie connections accumulating
- Before scheduled maintenance window

**Response:**
```json
{
  "Success": true,
  "CleanedConnections": 5,
  "ZombieConnections": 2,
  "Timestamp": "2025-12-02T12:00:00Z"
}
```

#### 3. Automated Cleanup (Recommended)
**Setup:** Azure Logic Apps or external cron job

**Recommended Schedule:** Every 5 minutes

**Azure Logic Apps Setup:**
1. Create new Logic App in Azure Portal
2. Add Recurrence trigger: Every 5 minutes
3. Add HTTP action:
   - Method: GET
   - URI: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/system/cleanup-connections`
4. Save and enable

**Cost:** ~$0/month (free tier: 200,000 actions/month, only need ~8,640/month)

### Troubleshooting Connection Issues

#### Issue: Database Never Pauses
**Symptoms:**
- Database always shows "Online" in Azure Portal
- Monthly cost remains at $50-175
- Expected auto-pause never occurs

**Diagnosis:**
```bash
# Check connection statistics
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/connection-statistics" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

**If CanAutoPause: false:**
1. Check `ConnectedUsers` count
2. Review `AuctionBreakdown` to see which auctions have active connections
3. Check `OldestActivity` timestamps - if very old, these are zombie connections

**Resolution:**
```bash
# Force cleanup
curl -X POST "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/cleanup-connections" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"

# Wait 15 minutes, then check Azure Portal to see if database pauses
```

#### Issue: Zombie Connections Accumulating
**Symptoms:**
- `ZombieConnections` count increasing over time
- Connections idle for 30+ minutes still marked as connected

**Possible Causes:**
- Automated cleanup not running
- Users closing browsers without proper disconnect
- Browser crashes or network interruptions

**Resolution:**
1. **Short-term:** Manually run cleanup endpoint
2. **Long-term:** Set up automated cleanup with Azure Logic Apps
3. **Verify:** Check cleanup is running regularly in Azure Logic Apps history

#### Issue: User Can't Reconnect
**Symptoms:**
- User tries to rejoin auction but connection fails
- Error message about existing connection

**Diagnosis:**
- User's previous connection still marked as active in database
- System thinks user is already connected

**Resolution:**
1. Run connection cleanup to clear stale connections
2. Have user wait 30 seconds and try again
3. If still failing, Auction Master can approve reconnection request manually

---

## Troubleshooting

### Common System Issues

#### Issue: Auction Master Locked Out
**Symptoms:**
- Creator lost browser session
- Cannot access WaitingRoomAdmin panel
- Management password not working for specific auction

**Resolution:**
1. **Use Master Recovery Code:**
   - Navigate to `/join`
   - Enter the auction's Master Recovery Code
   - System grants Auction Master role
   - Access WaitingRoomAdmin

2. **If Master Recovery Code Lost:**
   - Use system admin password to access system admin panel
   - View auction details to retrieve Master Recovery Code
   - Alternatively, manually assign Auction Master role to another user

#### Issue: All Participants Disconnected
**Symptoms:**
- No users showing as connected in WaitingRoomAdmin
- Auction appears frozen
- No one can bid

**Diagnosis:**
- SignalR service restart (rare)
- Network issue affecting all users
- Database pause during active auction (shouldn't happen but check Azure Portal)

**Resolution:**
1. **Pause auction** using Auction Master controls
2. Have all users refresh browser (F5)
3. Users will automatically reconnect
4. **Resume auction** when all users confirmed connected
5. Verify bidding state preserved

#### Issue: CSV Import Failing
**Symptoms:**
- Upload succeeds but no schools appear
- Error message during import
- Validation errors on school data

**Common Causes:**
1. **Missing required columns:**
   - Required: School, Conference, ProjectedPoints, LeagifyPosition
   - Check column names match exactly (case-sensitive)

2. **Invalid data types:**
   - ProjectedPoints must be numeric
   - NumberOfProspects must be integer
   - SchoolURL must be valid URL format

3. **Duplicate schools:**
   - Same school name appears multiple times
   - System enforces uniqueness per auction

**Resolution:**
1. Download template CSV from repository: `SampleDraftTemplate.csv`
2. Verify your CSV matches format exactly
3. Check for hidden characters or encoding issues (save as UTF-8)
4. Test with small subset of schools first (5-10 rows)

#### Issue: Budget Validation Blocking Bids
**Symptoms:**
- Users cannot bid even though they have sufficient total budget
- Error: "Cannot bid - would leave insufficient funds for remaining slots"

**Explanation:**
- This is intentional validation, not a bug
- System prevents bids that make roster completion mathematically impossible
- Formula: MaxBid = Budget - (EmptyRosterSlots - 1)

**Example:**
- User has $50 budget remaining
- User has 3 empty roster slots
- MaxBid = $50 - (3 - 1) = $48
- Cannot bid $49 or higher (would leave only $1 for 2 remaining slots)

**Resolution:**
- User must bid lower amount
- OR user should pass and wait for cheaper schools
- **Do not override** - this validation prevents stuck auctions

---

## Security & Maintenance

### Security Checklist

#### Authentication Security
- [ ] Management password uses strong format with timestamp
- [ ] Management password stored securely (not in code)
- [ ] Master Recovery Codes distributed only to trusted users
- [ ] Join codes kept private until auction starts

#### Data Protection
- [ ] HTTPS enforced for all traffic (automatic with Azure Static Web Apps)
- [ ] No sensitive data logged to console or application logs
- [ ] Database connection strings in environment variables only
- [ ] No user passwords (system uses join codes, not password authentication)

#### Access Control
- [ ] System admin access restricted to administrators only
- [ ] Auction Master role cannot access other auctions' admin panels
- [ ] Proxy Coaches can only bid for assigned teams
- [ ] Auction Viewers have read-only access enforced server-side

### Maintenance Tasks

#### Weekly Maintenance
**Check Connection Statistics:**
```bash
# Verify database auto-pause working correctly
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/connection-statistics" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

**Expected Results:**
- `CanAutoPause: true` when no active auctions
- `ZombieConnections: 0` (cleanup working correctly)
- Database shows "Paused" in Azure Portal during off-hours

#### Monthly Maintenance
**Archive Completed Auctions:**
1. Review auctions with Complete status
2. Archive auctions older than 30 days
3. Export results before archiving (just in case)

**Review Database Costs:**
1. Check Azure cost analysis for SQL Database
2. Should be $0-5/month with auto-pause working
3. If costs higher, investigate zombie connections

**Verify Automated Cleanup:**
1. Check Azure Logic Apps run history
2. Should show successful runs every 5 minutes
3. If failures, investigate error logs

#### Quarterly Maintenance
**Security Review:**
- Rotate management password
- Review audit logs for suspicious activity
- Check for any new Azure security recommendations

**Performance Review:**
- Database storage usage (should be well under 32 GB limit)
- Average response times for bid operations
- SignalR connection reliability

---

## Monitoring & Logs

### Azure Application Insights
**Setup:** (If not already configured)
1. Create Application Insights resource in Azure Portal
2. Add instrumentation key to Azure Static Web Apps configuration
3. Enable logging in Azure Functions

**Key Metrics to Monitor:**
- API response times
- Failed requests (4xx, 5xx errors)
- SignalR connection success rate
- Database query performance

### Azure Function Logs
**Accessing Logs:**
1. Azure Portal → Function App (within Static Web App)
2. Click "Log Stream" for real-time logs
3. Or use "Logs" for historical query

**Key Log Messages:**
```
🧹 Starting idle connection cleanup
✅ Cleaned up 5 idle connections (2 zombies)
⚠️ Invalid status transition from Draft to Complete
❌ Bid validation failed: insufficient budget
```

### Database Monitoring
**Azure SQL Analytics:**
1. Azure Portal → SQL Database → Monitoring
2. Key metrics:
   - DTU percentage (should be low with free tier)
   - Active connections count
   - Failed connection attempts
   - Storage usage (should grow slowly)

**Query Performance:**
- Most queries should complete in <100ms
- Bid operations critical path: <200ms total
- If slower, check for missing indexes or connection pool issues

### Health Check Endpoint
**Manual Health Check:**
```bash
# Verify API is responding
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/auction/health
```

**Expected Response:**
```json
{
  "Status": "Healthy",
  "Database": "Connected",
  "SignalR": "Available",
  "Timestamp": "2025-12-02T12:00:00Z"
}
```

---

## Quick Reference

### Essential URLs
- **System Admin**: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/management/system-admin`
- **Join Page**: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/join`
- **Connection Stats**: `GET /api/admin/connection-statistics`
- **Manual Cleanup**: `POST /api/admin/cleanup-connections`

### Critical Commands

**Check Connection Statistics:**
```bash
curl -X GET "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/connection-statistics" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

**Force Connection Cleanup:**
```bash
curl -X POST "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/admin/cleanup-connections" \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"
```

**Verify Database Status:**
1. Azure Portal → SQL databases → Overview
2. Status should be "Paused" during off-hours

### Emergency Procedures

#### Database Won't Pause (Costing Money)
1. Check connection statistics (see commands above)
2. Force cleanup if zombie connections found
3. Wait 15 minutes, verify database pauses in Azure Portal
4. If still not pausing, check Azure Logic Apps for cleanup schedule

#### Live Auction Critical Issue
1. Auction Master should pause auction immediately
2. Assess issue (connection, validation, bug)
3. Try standard troubleshooting (refresh browsers, reconnect users)
4. If unfixable: End auction early, export partial results, reschedule

#### Data Loss or Corruption Suspected
1. **Do not delete anything**
2. Check Azure SQL Database automatic backups (7 days retention)
3. Export current state for forensics
4. Contact Azure support if restore needed

### Support Resources
- **AUCTION-MASTER-GUIDE.md**: Guide for running individual auctions
- **AUCTION-CONTROL-TESTING.md**: Testing procedures for auction controls
- **CONNECTION-MANAGEMENT.md**: Detailed SignalR connection cleanup documentation
- **PRODUCT-DESIGN.md**: Technical architecture and design decisions
- **DATABASE-ERD.md**: Database schema and entity relationships
- **DEVELOPMENT-TASKS.md**: Implementation status and feature roadmap

---

**Version**: 1.0 (December 2, 2025)
**Last Updated**: Task 7.9 - Production Readiness documentation
**Next Review**: After first production auction with 6+ participants
