# Post-Deploy Test Plan for Auction Setup Wizard Fixes

## Quick 2-Minute API Test (Run Immediately After Deploy)

```bash
# Test 1: CSV endpoints should return 401 (exist, need auth)
echo "Testing CSV Preview endpoint:"
curl -sI -X POST https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/preview | head -1

echo "Testing CSV Confirm endpoint:"
curl -sI -X POST https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/confirm | head -1

# Test 2: GetAuctionById should return 401 (if fixed) or still 404 (deployment issue)
echo "Testing GetAuctionById endpoint:"
curl -sI https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1 | head -1

# Expected results:
# CSV endpoints: HTTP/2 401 (✅ WORKING)  
# GetAuctionById: HTTP/2 401 (✅ FIXED) or HTTP/2 404 (⚠️ still broken)
```

## Comprehensive UI Test Plan

### Step 1: Access Auction Management
1. Go to: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/management/auctions`
2. **Expected**: Page should load without "unhandled error"
3. **If error persists**: Frontend issue unrelated to our fixes - continue to direct wizard test

### Step 2: Direct Wizard Access Test  
1. Go to: `https://jolly-meadow-0b4450210.2.azurestaticapps.net/management/auctions/1/setup`
2. **Expected behavior**:
   - ✅ **If GetAuctionById fixed**: Loads wizard with auction details
   - ❌ **If still broken**: Shows "Auction not found" but this is a known deployment issue
3. **Action**: Continue to Step 3 regardless

### Step 3: CSV Upload Test (CRITICAL)
**This is the main fix we need to verify works**

1. Navigate to auction setup wizard Step 1 (Import Schools)
2. Click "Select CSV file" or similar upload button
3. Choose any small CSV file (even invalid format is fine for this test)
4. **CRITICAL CHECK**: 
   - ✅ **SUCCESS**: File uploads and shows processing/validation (may show format errors)
   - ❌ **FAILURE**: Immediate "Upload failed:" error before any processing

### Step 4: Authentication Test
If CSV upload appears to work but shows authentication errors:
1. Try to log in via `/management/login` with password: `LeagifyAdmin2024!`
2. If login works, retry CSV upload
3. If login doesn't work, this is a separate authentication issue

## Test Results Interpretation

### ✅ **Success Criteria Met**:
- CSV endpoints return 401 (not 404)
- CSV upload in wizard processes files instead of immediate "Upload failed"
- Users can select and upload CSV files in the auction setup wizard

### ⚠️ **Partial Success** (Still Usable):
- CSV upload works but GetAuctionById still returns 404
- Users might need to navigate directly to `/management/auctions/{id}/setup` 
- Wizard shows "Auction not found" but CSV upload functionality works

### ❌ **Failure** (Needs Investigation):
- CSV endpoints return 404 (routes still broken)
- CSV upload still shows immediate "Upload failed" error
- No improvement from previous state

## Manual Verification Commands

Run these to verify the exact endpoints the wizard calls:

```bash
# Verify exact routes match what SchoolImportStep.razor calls
echo "SchoolImportStep calls these routes (should be 401):"
curl -sI -X POST "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/preview"
curl -sI -X POST "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/confirm"

echo "AuctionSetup.razor calls this route:"
curl -sI "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1"
```

## Browser-Based Test
Open: `/test-csv-routes.html` in the deployed site to run automated endpoint tests

## Next Steps Based on Results

### If CSV Upload Works:
- ✅ **Primary objective achieved**
- Document any remaining GetAuctionById issues for future fix
- User can successfully import schools in auction setup wizard

### If CSV Upload Still Fails:
- Check if routes are returning 404 vs 401
- Verify Azure Functions deployment logs
- Consider route casing or parameter issues

### If Authentication Issues:
- Test management login functionality separately
- May need to investigate token storage/validation issues

**Priority: Focus on CSV upload functionality - this was the main user complaint that needed fixing.**