#!/bin/bash
# Connection Cleanup Testing Script
# Use this to verify SignalR connection management is working correctly

BASE_URL="https://jolly-meadow-0b4450210.2.azurestaticapps.net/api"

echo "=== Connection Cleanup Test Script ==="
echo "Testing at: $(date)"
echo ""

# Test 1: Check cleanup endpoint is working
echo "1. Testing cleanup endpoint..."
CLEANUP_RESULT=$(curl -s "$BASE_URL/system/cleanup-connections")
echo "   Response: $CLEANUP_RESULT"
echo ""

# Test 2: Check if there are any active connections
echo "2. Checking active connections via cleanup..."
CLEANED=$(echo "$CLEANUP_RESULT" | jq -r '.CleanedConnections // 0')
ZOMBIES=$(echo "$CLEANUP_RESULT" | jq -r '.ZombieConnections // 0')
echo "   Cleaned connections: $CLEANED"
echo "   Zombie connections: $ZOMBIES"
echo ""

# Test 3: Test auction join endpoint is responsive
echo "3. Testing auction join endpoint responsiveness..."
JOIN_RESPONSE=$(curl -s -w "\n   HTTP Status: %{http_code}" -X POST "$BASE_URL/auction/join" \
  -H "Content-Type: application/json" \
  -d '{"JoinCode":"TESTCODE","DisplayName":"TestUser"}')
echo "   Response: $JOIN_RESPONSE"
echo ""

echo "=== Manual Testing Steps ==="
echo "To fully verify connection management:"
echo ""
echo "1. CREATE AN AUCTION:"
echo "   - Go to $BASE_URL/../management/auctions"
echo "   - Login and create a test auction"
echo "   - Note the join code"
echo ""
echo "2. JOIN THE AUCTION:"
echo "   - Open a new incognito window"
echo "   - Go to the app and join with the code"
echo "   - You should see yourself as connected"
echo ""
echo "3. TEST DISCONNECT DETECTION:"
echo "   - Close the incognito window"
echo "   - In the admin panel, verify user shows as disconnected"
echo ""
echo "4. TEST CLEANUP:"
echo "   - Wait 10+ minutes without activity"
echo "   - Run: curl -s '$BASE_URL/system/cleanup-connections'"
echo "   - Verify CleanedConnections > 0"
echo ""
echo "5. TEST DATABASE AUTO-PAUSE:"
echo "   - Ensure no users are connected"
echo "   - Wait ~5 minutes"
echo "   - Check Azure portal - database should show as 'Paused'"
echo ""
echo "=== End of Script ==="
