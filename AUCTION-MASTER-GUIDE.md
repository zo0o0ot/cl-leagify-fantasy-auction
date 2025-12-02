# Auction Master Guide

## Table of Contents
1. [Pre-Auction Setup](#pre-auction-setup)
2. [Inviting Participants](#inviting-participants)
3. [Managing the Waiting Room](#managing-the-waiting-room)
4. [Starting the Auction](#starting-the-auction)
5. [Managing Live Bidding](#managing-live-bidding)
6. [Using Auction Controls](#using-auction-controls)
7. [Handling Common Issues](#handling-common-issues)
8. [Completing the Auction](#completing-the-auction)
9. [Quick Reference](#quick-reference)

---

## Pre-Auction Setup

### Step 1: Access System Admin
1. Navigate to `/management/system-admin`
2. Enter your management password
   - Password will be stored in your browser for future sessions
3. Verify you see the admin dashboard

### Step 2: Create New Auction
1. Click **"Create Auction"** button
2. Enter auction details:
   - **Auction Name**: Descriptive name (e.g., "2025 Spring Draft")
   - **Number of Teams**: How many teams will participate
   - **Budget Per Team**: Default $200, adjust if needed
3. Click **"Create"**
4. **Save the Join Code**: This 6-character code is how participants will join
5. **Save the Master Recovery Code**: 16-character backup code for admin access

### Step 3: Import School Data (CSV)
1. Prepare your CSV file following the template format:
   ```csv
   School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,LeagifyPosition
   Alabama,SEC,204,34,https://example.com/alabama-logo.svg,SEC
   Clemson,ACC,202,22,https://example.com/clemson-logo.svg,ACC
   Wisconsin,Big Ten,176,28,https://example.com/wisconsin-logo.svg,Big Ten
   ```
2. Click **"Import Schools"** button
3. Upload your CSV file
4. Review fuzzy match confirmations:
   - Green checkmarks = exact matches (auto-confirmed)
   - Yellow warnings = fuzzy matches (review and confirm/reject)
   - Red X = no match (will create new school entry)
5. Confirm school logo URLs are correct
6. Click **"Import All"** to finalize

**Validation Checks:**
- All required columns present
- Numeric fields are valid numbers
- No duplicate schools in same auction
- LeagifyPosition values match roster structure

### Step 4: Configure Roster Structure
1. Navigate to **"Roster Settings"**
2. Define position slots for each team based on conferences:
   - **Big Ten**: Schools from Big Ten Conference (typically 2 slots)
   - **SEC**: Schools from SEC Conference (typically 2 slots)
   - **Big 12**: Schools from Big 12 Conference (typically 1 slot)
   - **ACC**: Schools from ACC Conference (typically 1 slot)
   - **Small School**: Schools from smaller conferences combined (typically 1 slot)
   - **Flex**: Any school from any conference (typically 3 slots)
3. Position types are populated from the `LeagifyPosition` column in your CSV
4. Verify total roster slots × number of teams ≤ available schools
5. Click **"Save Roster Configuration"**

**Common Configuration (Most Common):**
- **6 teams, 10 slots each**: 60 total slots, need 60+ schools
  - Example roster per team: 2 Big Ten + 2 SEC + 1 Big 12 + 1 ACC + 1 Small School + 3 Flex = 10 slots

**Other Examples:**
- **8 teams, 10 slots each**: 80 total slots, need 80+ schools
- **10 teams, 10 slots each**: 100 total slots, need 100+ schools

### Step 5: Set Nomination Order
1. Go to **"Turn Order"** tab
2. Participants must have joined first (see next section)
3. Drag and drop users to set nomination order
4. **Important**: Only users with Team Coach or Proxy Coach roles can nominate
5. Click **"Save Turn Order"**

**Pro Tip**: Randomize turn order using a separate randomizer tool, then manually set the order in the system.

---

## Inviting Participants

### Sharing the Join Code
1. Share these details with participants:
   - **Join URL**: `https://your-app.azurestaticapps.net/join`
   - **Join Code**: The 6-character code from auction creation
   - **Instructions**: Enter join code + choose display name

### What Participants Will See
1. Enter join code (case-insensitive)
2. Enter display name (will be visible to all)
3. Automatically enters waiting room in "Auction Viewer" role
4. Wait for Auction Master to assign roles

### Managing Reconnections
If a participant loses connection:
1. They re-enter join code + same display name
2. **Reconnection Request** appears in your admin panel
3. Review the request:
   - Verify it's the correct person (use video call confirmation)
   - Check they're not already connected
4. Click **"Approve Reconnection"** or **"Deny"**

**Security Note**: Anyone with the join code can request to join. Always verify identity via video call before approving reconnections.

---

## Managing the Waiting Room

### Accessing WaitingRoomAdmin
1. Navigate to `/management/auction/{auctionId}/waiting-room-admin`
2. Opens in separate tab/window from your participant view
3. Shows real-time connection status of all participants

### Assigning Roles

**Available Roles:**

| Role | Permissions | Use Case |
|------|-------------|----------|
| **Auction Viewer** | Read-only access | Default role, observers, analysts |
| **Team Coach** | Nominate schools, place bids for own team | Standard participant |
| **Proxy Coach** | Bid for multiple teams, switch active team | Someone drafting for absent owner |
| **Auction Master** | Full control, override anything | You (only one per auction) |

**How to Assign Roles:**
1. Find user in participant list
2. Click **"Assign Role"** dropdown
3. Select role from list
4. Confirm assignment

**For Proxy Coaches:**
1. Assign "Proxy Coach" role
2. Click **"Manage Teams"** button
3. Check boxes for all teams they should control
4. Click **"Save Team Assignments"**

### Creating Team Names
Before auction starts, you must assign team names:
1. Click **"Auto-Generate Team Names"** for default names (Team 1, Team 2, etc.)
   OR
2. Click **"Edit Team Name"** for each participant to customize
3. Team names appear in bidding interface and final results

**Pro Tip**: Use owner names, city names, or fun team names for better engagement.

### Test Bidding (Optional)
Before starting the real auction, allow participants to practice:
1. Click **"Enable Test Bidding"**
2. Participants can nominate and bid on a test school
3. Monitor the UI to ensure everyone understands the interface
4. When done, click **"Reset Test Bids"** to clear all test data
   - ✅ Deletes all test bids
   - ✅ Resets HasTestedBidding flags
   - ✅ Resets IsReadyToDraft flags
   - ⚠️ Only available in **Draft** status

---

## Starting the Auction

### Pre-Start Checklist
Before clicking "Start Auction", verify:
- [ ] All participants have joined and have correct roles
- [ ] Team names are assigned
- [ ] Nomination order is set
- [ ] All Team Coaches understand bidding rules
- [ ] Proxy Coaches know which teams they control
- [ ] Everyone is on video call with shared screen
- [ ] You have WaitingRoomAdmin open in separate window

### Starting the Auction
1. Click **"Start Auction"** button in WaitingRoomAdmin
2. Status changes from **Draft** → **InProgress**
3. Auction cannot be reverted to Draft after starting
4. First user in nomination order can now nominate a school

### What Happens on Start
- ✅ Auction status becomes **InProgress**
- ✅ StartedDate timestamp recorded
- ✅ First nominator's turn activates
- ✅ All participants see live bidding interface
- ✅ Test bid buttons disappear

---

## Managing Live Bidding

### Normal Bidding Flow

**1. Nomination Phase**
- User whose turn it is clicks **"Nominate School"**
- Dropdown shows all available schools (not yet won by anyone)
- User selects school and confirms
- **Automatic $1 bid** placed by nominating user

**2. Bidding Phase**
- Any user with budget and open roster slots can bid
- Bids must be higher than current high bid
- System validates budget constraints:
  - MaxBid = Budget - (EmptyRosterSlots - 1)
  - Prevents bids that would make completing roster impossible
- Bidding continues until all users pass

**3. Passing**
- Users click **"Pass"** button when they don't want to bid higher
- Once all active bidders pass, current high bidder wins
- You can also click **"End Bidding"** to force completion

**4. School Assignment**
- System auto-assigns school to most restrictive valid position based on LeagifyPosition
  - Example: An SEC school goes to "SEC" slot before "Flex" slot
  - A Big Ten school goes to "Big Ten" slot before "Flex" slot
- Winner can override position assignment using dropdown (system only shows valid positions)
- Click **"Confirm Assignment"** to finalize

**5. Next Nomination**
- Turn advances to next user in nomination order
- Cycle repeats until all roster slots filled or no schools remain

### Your View vs. Participant View
**WaitingRoomAdmin Panel (Your View):**
- Connection status of all participants
- Current high bid and bidder
- Who has passed vs. still active
- Override controls (pause, end early, etc.)
- Source of truth for resolving disputes

**Participant View:**
- Current school being bid on
- Current high bid amount
- Their own budget and roster status
- Nominate/Bid/Pass buttons (context-dependent)

**Pro Tip**: Share your admin screen on video call so everyone sees the authoritative state.

---

## Using Auction Controls

### Pause Auction
**When to Use:**
- Technical difficulties (connection issues, browser crash)
- Need to discuss a rule clarification
- Restroom break or emergency
- Resolving a dispute

**How to Pause:**
1. Click **"Pause"** button in WaitingRoomAdmin header
2. Status changes from **InProgress** → **Paused**
3. All bidding stops immediately
4. Current auction state is preserved:
   - CurrentSchoolId remains set
   - CurrentHighBid unchanged
   - CurrentHighBidderUserId recorded

**What Participants See:**
- Status shows "Paused"
- Bid and Pass buttons disabled
- Current high bid still displayed

### Resume Auction
**How to Resume:**
1. Click **"Resume"** button (replaces Pause button when paused)
2. Status changes from **Paused** → **InProgress**
3. Bidding continues exactly where it left off
4. Same school, same high bid, same bidders

**No Data Loss:** Resume restores the exact auction state from before pause.

### End Auction Early
**When to Use:**
- Testing purposes (not real auction)
- Need to abandon auction due to technical issues
- Everyone agrees to stop early

**How to End Early:**
1. Click **"End Early"** button (available in **InProgress** or **Paused**)
2. Confirm you want to end the auction
3. Status changes to **Complete**
4. CompletedDate timestamp recorded
5. Auction stops accepting bids permanently

**⚠️ Warning:** This action cannot be undone. Auction cannot be resumed after ending early.

**Use Cases:**
- End test auction after verifying functionality
- Stop auction midway if major bug discovered
- Transition from Paused to Complete if deciding not to continue

### Reset Test Bids
**When to Use:**
- Cleaning up test bidding data between practice rounds
- Starting fresh after test auction

**How to Reset:**
1. Auction must be in **Draft** status (not available in InProgress/Paused)
2. Click **"Reset Test Bids"** button
3. Confirm reset action

**What Gets Reset:**
- ✅ All test bids deleted from BidHistory
- ✅ All users' HasTestedBidding = false
- ✅ All users' IsReadyToDraft = false
- ✅ All users' HasPassedOnTestBid = false
- ❌ Participants NOT removed (users remain in auction)
- ❌ Team names NOT reset (configuration preserved)
- ❌ Roster structure NOT reset

**Pro Tip**: Use this between multiple test runs without recreating the entire auction.

---

## Handling Common Issues

### Issue: Participant Can't See Auction
**Symptoms:** User joined but waiting room appears empty or frozen

**Troubleshooting:**
1. Check connection status in WaitingRoomAdmin
   - Green dot = connected
   - Red dot = disconnected
2. Ask user to refresh browser (F5)
3. If still not working, have them rejoin:
   - Re-enter join code + display name
   - Approve reconnection request in admin panel
4. Verify they're on correct URL (not localhost)

### Issue: Bid Button Disabled
**Possible Causes:**
1. **Not user's turn to bid**: Only nominator auto-bids $1, then anyone can bid
2. **Insufficient budget**: User doesn't have enough budget for valid bid
3. **No roster slots**: User's roster is full, can't nominate more schools
4. **Auction paused**: Bidding disabled until you click Resume

**How to Check:**
- Look at user's budget in admin panel
- Check their roster slot count
- Verify auction status is InProgress

### Issue: Budget Validation Failing
**Error:** "Cannot bid - would leave insufficient funds for remaining slots"

**Explanation:**
- System prevents bids that make completing roster mathematically impossible
- Formula: MaxBid = Budget - (EmptyRosterSlots - 1)
- Example: $50 budget, 3 empty slots → MaxBid = $50 - 2 = $48

**Resolution:**
- User must bid lower amount
- OR pass and try for cheaper schools later
- This is intentional validation, not a bug

### Issue: School Assignment to Wrong Position
**Symptoms:** School auto-assigned to Flex when user wanted it in a specific conference slot (e.g., SEC, Big Ten)

**Resolution:**
1. User should click **"Change Position"** dropdown
2. Select desired position (system only shows valid positions based on school's LeagifyPosition)
3. Click **"Confirm Assignment"**

**Note:** System auto-assigns to most restrictive position first (e.g., "SEC" before "Flex" for an SEC school), but user can always override to any valid position. This is strategy, not a bug - users can make suboptimal assignments (like putting an SEC school in Flex) if they choose.

### Issue: User Disconnected Mid-Auction
**Immediate Actions:**
1. Click **"Pause"** to stop bidding
2. Contact user via video call/phone
3. Have them rejoin via join code + display name
4. Approve reconnection in admin panel
5. Verify they see current auction state
6. Click **"Resume"** to continue

**State Synchronization:** Reconnecting users receive full current auction state via loading screen sync.

### Issue: Dispute Over High Bid
**Your Authority:** WaitingRoomAdmin view is source of truth

**Resolution Process:**
1. Check your admin panel for authoritative CurrentHighBid
2. Share screen showing your view on video call
3. If participant's view differs, have them refresh browser
4. If refresh doesn't fix it, pause and have them reconnect
5. Your view wins all disputes (document in rules before starting)

### Issue: Need to Override/Fix Something
**Manual Override Capabilities:**
1. **Force bid completion**: Click "End Bidding" button
2. **Pause for troubleshooting**: Use Pause control
3. **Skip problematic school**: (Not yet implemented - use workaround)
   - Pause auction
   - Contact winner via phone
   - Resume and manually confirm assignment

**Future Enhancements:**
- Assign school to specific team (manual override)
- Skip current school without awarding
- Adjust user budget mid-auction
- Edit turn order during auction

---

## Completing the Auction

### Natural Completion
Auction completes automatically when:
- All roster slots are filled across all teams
- OR no schools remain available to nominate
- OR all users have insufficient budget for minimum bids

**What Happens:**
- Status changes to **Complete**
- CompletedDate timestamp recorded
- No more nominations or bids accepted
- Export functionality becomes available

### Manual Completion
If you need to end auction early:
1. Click **"End Early"** button
2. Confirm action
3. Status → Complete

### Exporting Results
1. Navigate to **"Export Results"** tab
2. Click **"Generate CSV"** button
3. Downloads file matching SampleFantasyDraft.csv format:
   ```csv
   Team,School,Position,FinalBid,Conference,ProjectedPoints
   Team Alpha,Alabama,SEC,45,SEC,950
   Team Bravo,Wisconsin,Big Ten,38,Big Ten,820
   ```
4. Columns included:
   - Team name
   - School name
   - Assigned roster position (SEC, Big Ten, Flex, etc.)
   - Final winning bid
   - School metadata (conference, projected points, etc.)

### Post-Auction Review
After exporting results:
1. Share CSV with all participants
2. Archive auction in system admin (optional)
3. Review any issues that occurred for next auction improvement
4. Document any manual overrides or disputes in your records

**Pro Tip**: Keep master recovery code safe - it allows re-access if you lose management password.

---

## Quick Reference

### Auction Status Flow
```
Draft → InProgress → Complete
         ↓     ↑
       Paused ←┘
```

**Valid Transitions:**
- Draft → InProgress (Start Auction)
- InProgress → Paused (Pause button)
- InProgress → Complete (End Early or natural completion)
- Paused → InProgress (Resume button)
- Paused → Complete (End Early from pause)

### Essential URLs
- **System Admin**: `/management/system-admin`
- **Waiting Room Admin**: `/management/auction/{auctionId}/waiting-room-admin`
- **Join Page**: `/join` (share with participants)

### Key Validation Rules
- **Minimum bid**: CurrentHighBid + $1
- **Maximum bid**: Budget - (EmptyRosterSlots - 1)
- **Nomination eligibility**: Must have open roster slots
- **Auto-bid**: Nominating user automatically bids $1

### Emergency Procedures
1. **Auction frozen/stuck**: Pause → wait 10 seconds → Resume
2. **Multiple disconnections**: Pause → have all reconnect → verify states → Resume
3. **Need to restart**: End Early → create new auction → re-import CSV
4. **Database issues**: Contact system admin (technical support)

### Budget Math Quick Reference
| Budget | Empty Slots | Max Bid |
|--------|-------------|---------|
| $200   | 10          | $191    |
| $150   | 5           | $146    |
| $100   | 3           | $98     |
| $50    | 2           | $49     |
| $10    | 1           | $10     |

Formula: `MaxBid = Budget - (EmptySlots - 1)`

### Keyboard Shortcuts
- **F5**: Refresh participant view
- **Ctrl+R**: Refresh admin panel
- **Ctrl+Shift+T**: Reopen closed tab (if you accidentally close admin)

### Pre-Auction Checklist (Print This!)
```
□ Management password working
□ Auction created, join code saved
□ CSV imported successfully
□ Roster structure configured
□ All participants joined
□ Roles assigned correctly
□ Proxy coaches have correct team assignments
□ Team names set
□ Nomination order configured
□ Test bidding completed (if desired)
□ Test bids reset
□ Everyone on video call
□ Admin panel open in separate window/tab
□ Backup browser tab with join URL (in case admin tab crashes)
□ Master recovery code saved in safe place
```

### Post-Auction Checklist
```
□ Results exported to CSV
□ CSV shared with all participants
□ Any disputes documented
□ Manual overrides recorded
□ Auction archived (if desired)
□ Feedback collected for next auction
□ Next auction scheduled (if recurring)
```

---

## Additional Resources

- **AUCTION-CONTROL-TESTING.md**: Detailed testing guide for auction controls
- **CONNECTION-MANAGEMENT.md**: Understanding SignalR connection cleanup
- **PRODUCT-DESIGN.md**: Full technical architecture and design decisions
- **DATABASE-ERD.md**: Database schema and entity relationships
- **DEVELOPMENT-TASKS.md**: Implementation status and roadmap

---

## Getting Help

### During Live Auction
If you encounter a critical issue during a live auction:
1. **Pause immediately** using Pause button
2. **Check this guide** for troubleshooting steps
3. **Try basic fixes**: refresh browsers, reconnect users
4. **Last resort**: End Early and schedule makeup auction

### Technical Support
For bugs or system-level issues:
- GitHub Issues: https://github.com/anthropics/claude-code/issues (for Claude Code product issues)
- System admin contact: [Your technical support contact]
- Database issues: Check CONNECTION-MANAGEMENT.md for cleanup procedures

### Feature Requests
If you need functionality not yet implemented:
- Document your need for future development
- Check DEVELOPMENT-TASKS.md to see if it's already planned
- Many advanced features are in Phase 6-7 backlog

---

**Version**: 1.0 (December 2, 2025)
**Last Updated**: After implementing Task 7.2 (Auction Control Features)
**Next Review**: After Task 7.4 (First Full Test Auction with 6-8 participants)
