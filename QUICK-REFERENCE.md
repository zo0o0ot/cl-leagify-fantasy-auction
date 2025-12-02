# Quick Reference Cards

Print these cards for easy reference during auctions and system administration.

---

## Card 1: Auction Master - Pre-Flight Checklist

### Before Starting Auction

```
┌────────────────────────────────────────────────────────┐
│          PRE-AUCTION CHECKLIST (Print Me!)             │
├────────────────────────────────────────────────────────┤
│                                                        │
│ SETUP                                                  │
│ □ Management password working                          │
│ □ Auction created, join code saved                     │
│ □ CSV imported successfully                            │
│ □ Roster structure configured (6 teams × 10 slots)    │
│                                                        │
│ PARTICIPANTS                                           │
│ □ All participants joined                              │
│ □ Roles assigned correctly                             │
│ □ Proxy coaches have correct team assignments          │
│ □ Team names set                                       │
│                                                        │
│ AUCTION SETUP                                          │
│ □ Nomination order configured                          │
│ □ Test bidding completed (if desired)                  │
│ □ Test bids reset                                      │
│                                                        │
│ LIVE READINESS                                         │
│ □ Everyone on video call                               │
│ □ Admin panel open in separate window/tab             │
│ □ Backup browser tab with join URL ready              │
│ □ Master recovery code saved in safe place            │
│                                                        │
│ EMERGENCY CONTACTS                                     │
│ Tech Support: ___________________________________     │
│ Master Recovery Code: ____________________________     │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 2: Budget Validation Quick Reference

### Maximum Bid Calculator

```
┌────────────────────────────────────────────────────────┐
│              BUDGET MATH CHEAT SHEET                   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ FORMULA: MaxBid = Budget - (EmptySlots - 1)           │
│                                                        │
│ ┌────────────────────────────────────────────────┐   │
│ │ Budget │ Empty Slots │ Max Bid │ Why?         │   │
│ ├────────────────────────────────────────────────┤   │
│ │ $200   │ 10          │ $191    │ Start        │   │
│ │ $150   │ 5           │ $146    │ Mid-auction  │   │
│ │ $100   │ 3           │ $98     │ Late auction │   │
│ │ $50    │ 2           │ $49     │ Final slots  │   │
│ │ $10    │ 1           │ $10     │ Last slot    │   │
│ └────────────────────────────────────────────────┘   │
│                                                        │
│ QUICK CHECK:                                           │
│ • Must keep $1 minimum for each remaining slot         │
│ • Example: $50 left, 3 slots = keep $2, bid max $48   │
│ • If user can't bid, they need cheaper schools        │
│                                                        │
│ NOT A BUG - This prevents impossible auctions!        │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 3: Auction Control Buttons

### When to Use Each Control

```
┌────────────────────────────────────────────────────────┐
│              AUCTION CONTROL BUTTONS                   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ [PAUSE] Use when:                                      │
│   • Technical difficulties (connection, browser)       │
│   • Need to discuss rule clarification                 │
│   • Restroom break or emergency                        │
│   • Resolving a dispute                                │
│                                                        │
│   Effect: Stops bidding, preserves all state          │
│   Safe to use: YES - no data loss                     │
│                                                        │
│ ─────────────────────────────────────────────────────  │
│                                                        │
│ [RESUME] Use when:                                     │
│   • Ready to continue after pause                      │
│   • All users reconnected                              │
│   • Issue resolved                                     │
│                                                        │
│   Effect: Restores exact bidding state                │
│   Safe to use: YES - seamless continuation            │
│                                                        │
│ ─────────────────────────────────────────────────────  │
│                                                        │
│ [END EARLY] Use when:                                  │
│   • Test auction finished                              │
│   • Need to abandon due to technical issues            │
│   • Everyone agrees to stop                            │
│                                                        │
│   Effect: Marks auction Complete (cannot undo!)       │
│   Safe to use: NO - PERMANENT action                  │
│                                                        │
│ ─────────────────────────────────────────────────────  │
│                                                        │
│ [RESET TEST BIDS] Use when:                            │
│   • Cleaning up practice bids                          │
│   • Starting fresh test round                          │
│   • Only works in Draft status!                        │
│                                                        │
│   Effect: Deletes all test bids, keeps users          │
│   Safe to use: YES - only for testing phase           │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 4: Troubleshooting Decision Tree

### Quick Problem Resolution

```
┌────────────────────────────────────────────────────────┐
│           TROUBLESHOOTING FLOWCHART                    │
├────────────────────────────────────────────────────────┤
│                                                        │
│ User can't see auction?                                │
│   ├─→ Check connection status (green dot?)            │
│   ├─→ Ask user to refresh (F5)                        │
│   └─→ Have them rejoin (approve reconnection)         │
│                                                        │
│ Bid button disabled?                                   │
│   ├─→ Check if user's turn                            │
│   ├─→ Check budget sufficient?                        │
│   ├─→ Check roster not full?                          │
│   └─→ Check auction status = InProgress?              │
│                                                        │
│ "Insufficient funds" error?                            │
│   ├─→ This is CORRECT validation                      │
│   ├─→ User needs to bid lower                         │
│   └─→ Or pass and wait for cheaper schools            │
│                                                        │
│ User disconnected?                                     │
│   1. PAUSE auction                                     │
│   2. Contact user via video call                       │
│   3. User rejoins with join code                       │
│   4. Approve reconnection                              │
│   5. RESUME auction                                    │
│                                                        │
│ Dispute over high bid?                                 │
│   → YOUR view is source of truth                       │
│   → Share your screen on video call                    │
│   → Have participants refresh if different             │
│                                                        │
│ Everything broken?                                     │
│   1. PAUSE immediately                                 │
│   2. Try basic fixes (refresh, reconnect)              │
│   3. If unfixable: END EARLY and reschedule            │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 5: System Admin Commands

### Essential curl Commands

```
┌────────────────────────────────────────────────────────┐
│              SYSTEM ADMIN COMMANDS                     │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Set environment variable first:                        │
│   export MGMT_PWD="your-management-password"           │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ CHECK CONNECTION STATISTICS:                           │
│                                                        │
│ curl -X GET \                                          │
│   "https://jolly-meadow-0b4450210.2.azurestaticapps\ │
│    .net/api/admin/connection-statistics" \             │
│   -H "X-Management-Token: $MGMT_PWD"                   │
│                                                        │
│ Look for:                                              │
│   ✅ CanAutoPause: true (database will pause)          │
│   ⚠️  ZombieConnections > 0 (need cleanup)             │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ FORCE CONNECTION CLEANUP:                              │
│                                                        │
│ curl -X POST \                                         │
│   "https://jolly-meadow-0b4450210.2.azurestaticapps\ │
│    .net/api/admin/cleanup-connections" \               │
│   -H "X-Management-Token: $MGMT_PWD"                   │
│                                                        │
│ Use when:                                              │
│   • Database not auto-pausing                          │
│   • Zombie connections accumulating                    │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ VERIFY AUCTION STATUS:                                 │
│                                                        │
│ curl -X GET \                                          │
│   "https://jolly-meadow-0b4450210.2.azurestaticapps\ │
│    .net/api/auction/{auctionId}" \                     │
│   -H "X-Management-Token: $MGMT_PWD"                   │
│                                                        │
│ Shows: Status, CurrentSchoolId, CurrentHighBid, etc.   │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 6: Roster Configuration Guide

### Conference-Based Positions

```
┌────────────────────────────────────────────────────────┐
│           ROSTER STRUCTURE EXPLAINED                   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ IMPORTANT: You are drafting SCHOOLS, not players!     │
│                                                        │
│ Position Type    │ What It Means                      │
│ ─────────────────────────────────────────────────────  │
│ Big Ten          │ Schools from Big Ten Conference    │
│ SEC              │ Schools from SEC Conference        │
│ Big 12           │ Schools from Big 12 Conference     │
│ ACC              │ Schools from ACC Conference        │
│ Small School     │ Schools from smaller conferences   │
│ Flex             │ ANY school from ANY conference     │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ TYPICAL 10-SLOT ROSTER (6 teams):                     │
│                                                        │
│   2 × Big Ten slots                                    │
│   2 × SEC slots                                        │
│   1 × Big 12 slot                                      │
│   1 × ACC slot                                         │
│   1 × Small School slot                                │
│   3 × Flex slots                                       │
│   ─────────────────                                    │
│   10 total slots per team                              │
│                                                        │
│ With 6 teams: Need 60+ schools in CSV                 │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ POSITION ASSIGNMENT LOGIC:                             │
│                                                        │
│ • System auto-assigns to MOST RESTRICTIVE position    │
│ • Example: SEC school → "SEC" slot before "Flex"      │
│ • Users can override to any VALID position            │
│ • Putting SEC school in Flex is strategy, not bug!    │
│                                                        │
│ LeagifyPosition in CSV determines eligibility:        │
│   Alabama (LeagifyPosition: SEC) → SEC or Flex        │
│   Notre Dame (LeagifyPosition: Flex) → Flex only      │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 7: Status Transitions

### Valid Auction State Changes

```
┌────────────────────────────────────────────────────────┐
│              AUCTION STATUS FLOWCHART                  │
├────────────────────────────────────────────────────────┤
│                                                        │
│                  ┌─────────┐                           │
│                  │  Draft  │                           │
│                  └────┬────┘                           │
│                       │                                │
│                       │ [Start Auction]                │
│                       ↓                                │
│                ┌─────────────┐                         │
│           ┌───→│ InProgress  │←───┐                   │
│           │    └──────┬──────┘    │                   │
│           │           │            │                   │
│           │ [Resume]  │ [Pause]    │                   │
│           │           ↓            │                   │
│           │      ┌────────┐        │                   │
│           └──────│ Paused │────────┘                   │
│                  └───┬────┘                            │
│                      │                                 │
│       ┌──────────────┴──────────────┐                 │
│       │ [End Early]  │  [Natural]   │                 │
│       ↓              ↓               ↓                 │
│  ┌──────────────────────────────────────┐             │
│  │            Complete                  │             │
│  └────────────────┬─────────────────────┘             │
│                   │                                    │
│                   │ [Archive]                          │
│                   ↓                                    │
│            ┌────────────┐                              │
│            │  Archived  │ (Terminal - no exit)        │
│            └────────────┘                              │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ INVALID TRANSITIONS (will fail):                      │
│   ✗ Draft → Paused (must start first)                 │
│   ✗ Complete → InProgress (cannot resume finished)    │
│   ✗ Archived → anything (terminal state)              │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 8: Emergency Contact Card

### Keep This Handy During Live Auction

```
┌────────────────────────────────────────────────────────┐
│              EMERGENCY QUICK REFERENCE                 │
├────────────────────────────────────────────────────────┤
│                                                        │
│ CRITICAL URLS:                                         │
│                                                        │
│ System Admin:                                          │
│   https://jolly-meadow-0b4450210.2.azurestaticapps   │
│   .net/management/system-admin                         │
│                                                        │
│ Join Page:                                             │
│   https://jolly-meadow-0b4450210.2.azurestaticapps   │
│   .net/join                                            │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ YOUR AUCTION:                                          │
│                                                        │
│ Auction ID: _______                                    │
│ Join Code: _______                                     │
│ Master Recovery Code: ______________________________   │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ EMERGENCY PROCEDURES:                                  │
│                                                        │
│ 🚨 Auction frozen/stuck:                               │
│    1. PAUSE                                            │
│    2. Wait 10 seconds                                  │
│    3. RESUME                                           │
│                                                        │
│ 🚨 Multiple users disconnected:                        │
│    1. PAUSE immediately                                │
│    2. Have all users reconnect                         │
│    3. Verify everyone sees correct state               │
│    4. RESUME                                           │
│                                                        │
│ 🚨 Cannot continue (critical bug):                     │
│    1. END EARLY                                        │
│    2. Export partial results                           │
│    3. Reschedule with new auction                      │
│                                                        │
│ 🚨 Locked out (lost admin access):                     │
│    • Use Master Recovery Code at /join page           │
│    • System grants Auction Master role automatically   │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ SUPPORT CONTACTS:                                      │
│                                                        │
│ Tech Support: ___________________________________     │
│ Phone: ___________________________________________     │
│ Backup Auction Master: ___________________________     │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Card 9: Post-Auction Checklist

### After Auction Completes

```
┌────────────────────────────────────────────────────────┐
│          POST-AUCTION CHECKLIST (Print Me!)            │
├────────────────────────────────────────────────────────┤
│                                                        │
│ IMMEDIATE (Within 5 minutes):                          │
│ □ Export results to CSV                                │
│ □ Verify CSV contains all teams and schools           │
│ □ Share CSV with all participants                      │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ SAME DAY:                                              │
│ □ Document any disputes that occurred                  │
│ □ Record any manual overrides or fixes                │
│ □ Note any bugs or issues found                        │
│ □ Collect participant feedback                         │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ WITHIN 1 WEEK:                                         │
│ □ Archive auction in system admin                      │
│ □ Review lessons learned                               │
│ □ Update documentation if needed                       │
│ □ File bug reports for any issues found               │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ NEXT AUCTION PREP:                                     │
│ □ Apply lessons learned                                │
│ □ Test any bug fixes                                   │
│ □ Update pre-auction checklist if needed              │
│ □ Schedule next auction date                           │
│                                                        │
│ ──────────────────────────────────────────────────────│
│                                                        │
│ KEY METRICS TO TRACK:                                  │
│                                                        │
│ • Total auction duration: _____ minutes                │
│ • Number of pauses needed: _____                       │
│ • Number of reconnections: _____                       │
│ • Technical issues encountered: _____                  │
│ • Participant satisfaction (1-5): _____                │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

## Printing Instructions

### How to Use These Cards

1. **Print Individual Cards**: Each card is designed to fit on a single page
2. **Laminate for Durability**: Recommended for frequently used cards
3. **Keep at Workstation**: Have cards visible during live auctions
4. **Share with Team**: Give copies to backup Auction Masters

### Recommended Cards to Print

**For Auction Masters:**
- Card 1: Pre-Flight Checklist
- Card 2: Budget Math
- Card 3: Auction Controls
- Card 4: Troubleshooting
- Card 8: Emergency Contact
- Card 9: Post-Auction Checklist

**For System Admins:**
- Card 5: System Admin Commands
- Card 6: Roster Configuration
- Card 7: Status Transitions

### Digital Use

Keep this file open in a separate browser tab during auctions for quick reference. Use Ctrl+F to search for specific terms.

---

**Version**: 1.0 (December 2, 2025)
**Part of**: Task 7.9 - Production Readiness Documentation
