# Auction Setup Wizard Fix Test Plan

## Issues Fixed

### 1. CSV Upload Route Mismatch ✅
**Problem**: SchoolImportStep was calling wrong API routes
- Called: `/api/management/auctions/{id}/csv-import/preview`
- Should be: `/api/management/auctions/{id}/csv/preview`

**Fix**: Updated routes in SchoolImportStep.razor to match API endpoints

### 2. Missing GetAuctionById Endpoint ✅
**Problem**: AuctionSetup.razor couldn't load auction details
- Called: `/api/management/auctions/{id}` (didn't exist)
- Result: "Auction not found or you don't have permission to access it"

**Fix**: Added GetAuctionById endpoint to AuctionManagementFunction.cs

## Test Instructions (After 6-minute deployment)

### Test 1: Verify API Endpoints Work
```bash
# Test GetAuctionById endpoint exists (should return 401 without token)
curl -I https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1

# Test CSV preview endpoint exists (should return 401 without token) 
curl -I -X POST https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/preview
```

### Test 2: Manual UI Test
1. **Navigate to auction setup wizard**:
   - Go to `/management/auctions`
   - Click "Configure auction" (⚙️) button on any existing auction
   - Should load `/management/auctions/{id}/setup` WITHOUT "Auction not found" error

2. **Test CSV upload**:
   - Should reach Step 1: Import Schools 
   - Select a CSV file
   - Should NOT show "Upload failed:" error immediately
   - May show authentication error if not logged in properly

### Test 3: Expected Behavior
✅ **Before Fix**: 
- "Auction not found or you don't have permission to access it" 
- CSV upload immediately failed with "Upload failed:"

✅ **After Fix**:
- Auction wizard loads properly with auction name in header
- CSV upload processes (may still need authentication token)
- Step navigation works correctly

## Authentication Note
The management system requires a token stored in localStorage. The master password is "LeagifyAdmin2024!" but the login endpoint may have separate issues.

## Verification Commands
Run these after 6-minute deployment window:

```bash
# 1. Check GetAuctionById endpoint exists
curl -sI https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1 | head -1

# 2. Check CSV preview endpoint exists  
curl -sI -X POST https://jolly-meadow-0b4450210.2.azurestaticapps.net/api/management/auctions/1/csv/preview | head -1

# Both should return "HTTP/2 401" (unauthorized) instead of 404 (not found)
```