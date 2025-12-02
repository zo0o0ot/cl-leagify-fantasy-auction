# Auction Control Features - Testing Guide

## Overview
Testing guide for Task 7.2 auction control features: Pause, Resume, End Early, and Reset Test Bids.

**Deployment Date:** December 2, 2025
**Features Added:** Pause/Resume/End/Reset controls for safe auction testing

---

## Prerequisites

### Required Access
- Management admin credentials (stored in localStorage as `managementToken`)
- Access to WaitingRoomAdmin panel: `/management/auction/{auctionId}/waiting-room-admin`
- At least one test auction in Draft or InProgress status

### Setup Steps
1. Navigate to `/management/system-admin`
2. Enter management token (format: `admin:YYYY-MM-DDTHH:MM:SSZ` base64-encoded)
3. Create or select a test auction
4. Have 2-3 browser windows/tabs ready for multi-user testing

---

## Test Scenarios

### Test 1: Pause Active Auction

**Purpose:** Verify auction can be paused mid-bidding without losing state

**Steps:**
1. Start an auction (Status: Draft → InProgress)
2. Have a test school nominated with active bidding
3. Note current state:
   - Current school being bid on
   - Current high bid amount
   - Current high bidder
4. Click **"Pause"** button in WaitingRoomAdmin
5. Wait for confirmation

**Expected Results:**
✅ Status changes to "Paused"
✅ Pause button disappears, Resume button appears
✅ Auction state preserved:
   - CurrentSchoolId remains set
   - CurrentHighBid remains unchanged
   - CurrentHighBidderUserId still recorded
✅ All participants see paused status
✅ No errors in browser console

**Verification:**
```bash
# Check auction status via API
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/auction/{auctionId} \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"

# Should show:
# "Status": "Paused"
# "CurrentSchoolId": <not null>
# "CurrentHighBid": <preserved value>
```

**Edge Cases:**
- Try pausing when no active bidding (CurrentSchoolId = null) - should still work
- Try pausing from Draft status - should fail with error
- Try pausing already-paused auction - should fail gracefully

---

### Test 2: Resume Paused Auction

**Purpose:** Verify auction resumes with preserved state

**Steps:**
1. Start with a paused auction (from Test 1)
2. Verify state is still preserved (CurrentSchoolId, CurrentHighBid)
3. Click **"Resume"** button in WaitingRoomAdmin
4. Wait for confirmation

**Expected Results:**
✅ Status changes to "InProgress"
✅ Resume button disappears, Pause button reappears
✅ Bidding state fully restored:
   - Same school still being bid on
   - Same high bid amount
   - Same high bidder
✅ Participants can continue bidding from where they left off
✅ No data loss or corruption

**Verification:**
```bash
# Check auction status
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/auction/{auctionId}

# Should show:
# "Status": "InProgress"
# "CurrentSchoolId": <same as before pause>
# "CurrentHighBid": <same as before pause>
```

**Edge Cases:**
- Resume after 1 minute pause - should work
- Resume after 10+ minute pause - should work
- Try resuming InProgress auction - should fail
- Try resuming Complete auction - should fail

---

### Test 3: End Auction Early (from InProgress)

**Purpose:** Verify auction can be terminated early for testing

**Steps:**
1. Start an auction with active bidding
2. Click **"End Early"** button
3. Confirm you want to end the auction

**Expected Results:**
✅ Status changes to "Complete"
✅ All control buttons disappear (except those for Complete status)
✅ CompletedDate timestamp set
✅ Auction stops accepting new bids
✅ Participants see completed status

**Verification:**
```bash
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/auction/{auctionId}

# Should show:
# "Status": "Complete"
# "CompletedDate": <timestamp>
```

**Edge Cases:**
- End auction with active bidding - should complete cleanly
- End auction with no bids yet - should work
- Try ending Draft auction - should fail (Complete transition not allowed from Draft)

---

### Test 4: End Auction Early (from Paused)

**Purpose:** Verify auction can be terminated from paused state

**Steps:**
1. Start auction, pause it
2. Click **"End Early"** button while paused
3. Confirm action

**Expected Results:**
✅ Status changes from Paused → Complete (skipping InProgress)
✅ CompletedDate set
✅ No Resume option available
✅ Auction permanently ended

**Verification:**
- Check status is "Complete"
- Verify cannot resume after ending from pause
- Check participants see final status

---

### Test 5: Reset Test Bids (Draft Status)

**Purpose:** Clean up test bidding data between testing rounds

**Prerequisites:**
- Auction in Draft status
- Some test bids placed by participants
- Some users marked as "HasTestedBidding" or "IsReadyToDraft"

**Steps:**
1. Navigate to WaitingRoomAdmin for Draft auction
2. Verify test bids exist in BidHistory table
3. Click **"Reset Test Bids"** button
4. Wait for confirmation

**Expected Results:**
✅ All test bids deleted from database
✅ All users' HasTestedBidding = false
✅ All users' IsReadyToDraft = false
✅ All users' HasPassedOnTestBid = false
✅ Test school bidding can start fresh
✅ Participant count unchanged (users not removed)

**Verification:**
```bash
# Check test bids are gone
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/auction/{auctionId}/test-bid/history

# Should return empty or minimal data
```

**Edge Cases:**
- Reset with no test bids - should succeed with 0 deleted
- Reset with 100+ test bids - should delete all
- Try reset on InProgress auction - button should not appear
- Try reset on Paused auction - button should not appear

---

### Test 6: Use Standard 10-Slot Roster (Quick Setup)

**Purpose:** Verify one-click standard roster creation

**Prerequisites:**
- Auction in Draft status
- No roster positions configured yet
- Access to auction setup page

**Steps:**
1. Create a new auction via System Admin
2. Navigate to `/management/auctions/{auctionId}/setup`
3. Skip CSV import or import schools (both scenarios work)
4. Go to "Configure Roster" step (step 2)
5. Verify green "Quick Setup" card is visible
6. Click **"Use Standard 10-Slot Roster"** button
7. Wait for confirmation alert

**Expected Results:**
✅ Green "Quick Setup" card appears when no roster positions exist
✅ Button shows loading state while creating positions
✅ Success alert: "Standard 10-slot roster created successfully!"
✅ 6 roster positions created automatically:
   - Big Ten (2 slots, not flex)
   - SEC (2 slots, not flex)
   - Big 12 (1 slot, not flex)
   - ACC (1 slot, not flex)
   - Small School (1 slot, not flex)
   - Flex (3 slots, IS flex position)
✅ Total: 10 slots per team
✅ Each position has unique color from default palette
✅ Positions appear in correct order
✅ Quick setup card disappears after positions created

**Verification:**
```bash
# Check roster positions via API
curl https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/{auctionId}/roster-positions \
  -H "X-Management-Token: $MANAGEMENT_PASSWORD"

# Should return 6 positions with correct names and slot counts
```

**Manual Verification:**
- Roster summary shows "10 schools total"
- 5 specific positions (Big Ten, SEC, Big 12, ACC, Small School)
- 3 flex slots
- Each position has color indicator
- Can edit, reorder, or delete positions after creation

**Edge Cases:**
- Button only appears when rosterPositions.Count == 0
- After creating positions, button disappears (card hidden)
- Can delete all positions to see button again
- Works whether CSV imported or not
- Works with or without schools imported

**UI/UX Checks:**
- Green card with sparkle icon is visually prominent
- Button text is clear: "Use Standard 10-Slot Roster"
- Description explains structure: "2 Big Ten + 2 SEC + 1 Big 12 + 1 ACC + 1 Small School + 3 Flex = 10 slots"
- Loading spinner appears while creating positions
- No errors in browser console

---

## Multi-User Testing

### Scenario: Pause During Active Bidding

**Participants:** 3 users (User A, User B, Auction Master)

**Steps:**
1. User A nominates School X, auto-bids $1
2. User B bids $5
3. User A counter-bids $10
4. **Auction Master pauses** via admin panel
5. User A tries to bid $15 (should fail or be blocked)
6. **Auction Master resumes**
7. User A bids $15 (should succeed)
8. Verify all users see consistent state

**Expected Results:**
- All users see "Paused" status simultaneously
- No bids accepted while paused
- Resume allows bidding to continue seamlessly
- No race conditions or state corruption

---

## State Transition Matrix

Test all valid and invalid transitions:

| From Status | To Status | Method | Should Work? |
|-------------|-----------|---------|--------------|
| Draft | InProgress | Start Auction | ✅ Yes |
| Draft | Paused | Pause | ❌ No (Invalid) |
| InProgress | Paused | Pause | ✅ Yes |
| InProgress | Complete | End Early | ✅ Yes |
| Paused | InProgress | Resume | ✅ Yes |
| Paused | Complete | End Early | ✅ Yes |
| Paused | Draft | Resume | ❌ No (Invalid) |
| Complete | Paused | Pause | ❌ No (Invalid) |
| Complete | InProgress | Resume | ❌ No (Invalid) |
| Complete | Archived | (Future) | ✅ Yes |

**Test Each Invalid Transition:**
- Verify appropriate error message
- Verify status unchanged
- Verify no corruption

---

## API Endpoint Testing

### Manual API Testing (with curl)

```bash
# Set your management token
export MGMT_TOKEN="your-base64-encoded-token"

# Test Pause
curl -X POST \
  "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/52/pause" \
  -H "X-Management-Token: $MGMT_TOKEN" \
  -v

# Expected: 200 OK with {"Success":true,"Status":"Paused",...}

# Test Resume
curl -X POST \
  "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/52/resume" \
  -H "X-Management-Token: $MGMT_TOKEN" \
  -v

# Expected: 200 OK with {"Success":true,"Status":"InProgress",...}

# Test End Early
curl -X POST \
  "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/52/end" \
  -H "X-Management-Token: $MGMT_TOKEN" \
  -v

# Expected: 200 OK with {"Success":true,"Status":"Complete",...}

# Test Without Auth (should fail)
curl -X POST \
  "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/52/pause" \
  -v

# Expected: 401 Unauthorized
```

---

## Performance Testing

### Load Testing Scenarios

**Test:** Rapid pause/resume cycles
```
1. Pause auction
2. Wait 2 seconds
3. Resume auction
4. Wait 2 seconds
5. Repeat 10 times
```

**Expected:** No degradation, no stuck states

**Test:** Concurrent control requests
- Have 2 admins try to pause simultaneously
- Verify only one succeeds
- Verify no database corruption

---

## Troubleshooting

### Issue: "Unauthorized" error when clicking buttons

**Cause:** Management token not in localStorage
**Fix:**
1. Go to `/management/system-admin`
2. Re-enter management token
3. Token will be stored and reused

### Issue: Buttons not appearing

**Cause:** Incorrect auction status or UI not refreshed
**Fix:**
1. Click "Refresh" button to reload status
2. Verify auction status via API
3. Check browser console for errors

### Issue: State not preserved after pause/resume

**Cause:** Database update failure or race condition
**Fix:**
1. Check Azure Function logs for errors
2. Verify database connectivity
3. Check CurrentSchoolId/CurrentHighBid directly in database

### Issue: Participants don't see status change

**Cause:** SignalR broadcast not implemented yet (expected)
**Fix:**
- Participants need to refresh browser manually
- SignalR broadcasts will be added in future enhancement

---

## Database Verification Queries

If you have direct database access, verify state:

```sql
-- Check auction status
SELECT AuctionId, Status, CurrentSchoolId, CurrentHighBid,
       StartedDate, CompletedDate, ModifiedDate
FROM Auctions
WHERE AuctionId = 52;

-- Check test bids before/after reset
SELECT COUNT(*) as TestBidCount
FROM BidHistory
WHERE AuctionId = 52 AND BidType = 'TestBid';

-- Check user readiness flags
SELECT UserId, DisplayName, HasTestedBidding, IsReadyToDraft
FROM Users
WHERE AuctionId = 52;
```

---

## Success Criteria Checklist

Before considering testing complete, verify:

- [ ] Can pause InProgress auction
- [ ] Can resume Paused auction
- [ ] Can end InProgress auction early
- [ ] Can end Paused auction early
- [ ] Can reset test bids in Draft status
- [ ] Can create standard 10-slot roster with one click
- [ ] Standard roster creates correct 6 positions with proper slot counts
- [ ] Invalid transitions properly rejected
- [ ] Bidding state preserved across pause/resume
- [ ] No console errors during any operation
- [ ] Buttons appear/disappear correctly based on status
- [ ] Management auth properly enforced
- [ ] Multi-user testing shows consistent state
- [ ] Rapid pause/resume cycles work without issues
- [ ] Database state remains consistent

---

## Next Steps After Testing

Once testing is complete:

1. **Document any bugs found** in GitHub issues
2. **Update DEVELOPMENT-TASKS.md** - Mark Task 7.2 as complete
3. **Proceed to Task 7.4** - First Full Test Auction with 6-8 participants
4. **Consider enhancements:**
   - Add confirmation dialogs for destructive actions (End Early)
   - Add SignalR broadcasts for real-time status updates
   - Add audit logging for all control actions
   - Add "Reason" field for pause/end (optional)

---

## Quick Test Script

For a fast smoke test (5-7 minutes):

```bash
# 1. Create test auction (Draft status)
# 2. Go to roster configuration → Click "Use Standard 10-Slot Roster"
# 3. Verify 6 positions created (Big Ten×2, SEC×2, Big 12, ACC, Small School, Flex×3)
# 4. Complete setup and start auction → InProgress
# 5. Pause → verify status "Paused"
# 6. Resume → verify status "InProgress"
# 7. Pause again
# 8. End Early from Paused → verify status "Complete"
# 9. Create new Draft auction
# 10. Add test bids
# 11. Reset Test Bids → verify bids cleared
```

All operations should complete without errors in under 7 minutes.

---

## Test Results Template

Use this template to document your testing:

```markdown
## Test Results - [Date]

**Tester:** [Name]
**Environment:** Production / Test
**Auction ID:** [ID]

### Test 1: Pause Active Auction
- Status: ✅ Pass / ❌ Fail
- Notes:

### Test 2: Resume Paused Auction
- Status: ✅ Pass / ❌ Fail
- Notes:

### Test 3: End Auction Early (InProgress)
- Status: ✅ Pass / ❌ Fail
- Notes:

### Test 4: End Auction Early (Paused)
- Status: ✅ Pass / ❌ Fail
- Notes:

### Test 5: Reset Test Bids
- Status: ✅ Pass / ❌ Fail
- Notes:

### Test 6: Use Standard 10-Slot Roster
- Status: ✅ Pass / ❌ Fail
- Notes:

### Bugs Found:
1.
2.

### Overall Assessment:
- Ready for production: Yes / No
- Blocking issues: None / [List]
```

---

**Happy Testing! 🧪**

When you're ready to test, just follow the scenarios above and document any issues you find.
