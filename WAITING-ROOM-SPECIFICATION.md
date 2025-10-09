# Auction Waiting Room & Role Assignment Specification

## Overview
The waiting room serves as a pre-auction testing and preparation area where participants verify their technical setup, get familiar with the bidding interface, and confirm readiness before the live auction begins.

## User Stories & Workflow Context

### Typical Participants
- **AJ**: Reliable team coach, usually present for entire auction
- **Tilo**: Usually on time but may need to leave early
- **Jared**: Busy with kids, sometimes joins late
- **Netten**: Sometimes forgets to attend
- **Ross**: Auction Master who manages everything and proxies for absent coaches

### Real-World Context
- All participants are typically on a video call (Zoom, Google Meet, etc.)
- Connection status is less critical since verbal communication handles most issues
- Role assignments are coordinated through voice communication
- Technical disconnections vs actual departures are distinguished via voice

## Auction Status Flow

```
Setup → Waiting Room → Live Auction
  ↑         ↑            ↑
Ross     Testing &    Real bidding
configs  Role Assign   begins
```

## Waiting Room Features

### Test Bidding System

**Test Parameters:**
- **Test School**: "Vermont A&M" (fixed, not configurable)
- **Test Budget**: $200 per team (matches typical real auction budget)
- **Test Rules**: Same bidding interface as real auction, no consequences

**Team Coach Interface:**
```
┌─── AUCTION WAITING ROOM - TEST BIDDING ───────────────────────┐
│ Status: Testing Phase - Practice bidding on "Vermont A&M"     │
│                                                                │
│ Current Test Bid: $15 (AJ)                                   │
│ Your Test Budget: $200 | Remaining: $185                      │
│                                                                │
│ [Bid $16] [Bid $20] [Bid $25] [Pass]                         │
│                                                                │
│ Recent Test Bids:                                             │
│ • AJ bid $15                                                  │
│ • Tilo bid $12                                               │
│ • Ross (for Netten) bid $10                                  │
│                                                                │
│ Ready Status: [✅ I'm Ready to Draft] [❌ Still Testing]      │
└───────────────────────────────────────────────────────────────┘
```

### School Information Display

**Read-Only School List:**
- Full list of schools available for auction
- Visible for strategy planning and familiarization
- Cannot bid on these schools (only Vermont A&M for testing)
- Helps coaches prepare their draft strategy

**Display Format:**
```
┌─── AVAILABLE SCHOOLS (Read Only) ─────────────────────────────┐
│ 🏫 Alabama A&M          │ 🏫 Clemson                │ 🏫 Duke    │
│ 🏫 Auburn               │ 🏫 Florida State          │ 🏫 Georgia │
│ 🏫 Boston College       │ 🏫 Louisville             │ ... etc    │
│                                                                │
│ Note: Practice bidding only available on Vermont A&M          │
└───────────────────────────────────────────────────────────────┘
```

### Nomination Order Display

**Draft Order Visibility:**
- Shows complete nomination order to all participants
- Updates dynamically to remove coaches who have filled their rosters
- Helps coaches prepare for their upcoming turns

```
┌─── DRAFT ORDER ───────────────────────────────────────────────┐
│ Next Up: 1. Tilo → 2. AJ → 3. Jared → 4. Netten → 5. Ross    │
│                                                                │
│ • Based on last year's reverse standings                      │
│ • Order updates as teams complete their rosters               │
└───────────────────────────────────────────────────────────────┘
```

## Role Assignment System

### Role Types
1. **Auction Master**: Ross (full control, can hold multiple proxy roles)
2. **Team Coach**: Active participant managing their own team
3. **Proxy Coach**: Ross managing one or more teams for absent/departed coaches
4. **Waiting Team Coach**: Late joiner queued for next natural break point

### Dual Readiness System

**Technical Readiness (Automatic):**
- ❓ **Not Tested**: Haven't placed any test bids
- ✅ **Bidding Tested**: Successfully placed at least one test bid

**Personal Readiness (Manual):**
- ❓ **Still Testing**: Default state or clicked "Still Testing"
- 🏈 **Ready to Draft**: Clicked "I'm Ready to Draft" button

**Combined Status Indicators:**
- **❓❓**: Not tested, not ready (just joined)
- **✅❓**: Tested bidding, still getting comfortable
- **✅🏈**: Tested and ready to go

### Auction Master Dashboard

**Ross's Control Interface:**
```
┌─── WAITING ROOM - DRAFT READINESS ────────────────────────────┐
│ Team Readiness:                                               │
│ ✅🏈 AJ - Bidding tested ✓ Ready to draft ✓                  │
│ ✅❓ Tilo - Bidding tested ✓ Still testing...                │
│ ❓❓ Jared - Not tested yet, not ready                        │
│ ✅🏈 Netten (Ross Proxy) - Tested ✓ Ready ✓                  │
│                                                               │
│ Vermont A&M Test Activity: 8 bids placed                     │
│ Teams Ready: 2/4  |  Teams Tested: 3/4                      │
│                                                               │
│ [Reset Test Bidding] [Start Real Auction] [Stay in Testing]  │
└───────────────────────────────────────────────────────────────┘
```

### Role Assignment Controls

**Quick Assignment Actions:**
- **[Take Proxy for X]**: Ross becomes proxy for any team
- **[Release Proxy]**: Return control to original coach
- **[Assign Now]**: Override waiting period for late joiners
- **Manual role switching**: Based on verbal coordination

**Multi-Proxy Support:**
- Ross can simultaneously proxy multiple teams
- Clear visual indication of which teams Ross is managing
- Easy switching between proxy assignments

## Budget Management

### Default Settings
- **Standard Budget**: $200 per team (configurable by auction)
- **Test Budget**: Always $200 (matches real auction for consistency)

### Penalty System (Waiting Room Only)
- **Text field input**: Ross can adjust any team's budget
- **Common use cases**: Late arrival penalties, rule infractions
- **Example**: Change budget from $200 to $190 for late arrival
- **Timing**: Only available during waiting room phase

**Budget Adjustment Interface:**
```
┌─── TEAM BUDGET ADJUSTMENTS ───────────────────────────────────┐
│ AJ:     [$200] (Default)                                      │
│ Tilo:   [$190] (Late penalty)                                 │
│ Jared:  [$200] (Default)                                      │
│ Netten: [$200] (Default)                                      │
│ Ross:   [$200] (Default)                                      │
└───────────────────────────────────────────────────────────────┘
```

## Reconnection Handling

### Mid-Auction Reconnection Flow
1. **Returning participant sees welcome screen**
2. **Brief auction state summary**: "Ross managed your team while away"
3. **Direct entry to live auction**: Skip waiting room for reconnections
4. **Future enhancement**: Show reconnection stats (what happened while away)

### Disconnection Management
- **Ross decides**: Pause auction vs take proxy (based on voice communication)
- **No automated responses**: Manual control preferred
- **Verbal coordination**: "Tilo, you still there?" determines action

## Technical Implementation Notes

### Database Structure
- **Teams**: Pre-created with known participants
- **UserRoles**: Links users to teams and role types
- **TestBidding**: Separate from real auction data
- **BudgetAdjustments**: Track any penalty modifications

### State Management
- **Waiting Room State**: Testing phase before live auction
- **Ready Status**: Individual coach readiness flags
- **Proxy Assignments**: Track Ross's multiple proxy roles
- **Test Data Isolation**: Vermont A&M bids don't affect real auction

### User Experience Flow

**New Participant (AJ):**
1. Joins with join code
2. Gets assigned to "Team AJ" by Ross
3. Sees waiting room with Vermont A&M test bidding
4. Places test bids, sees school list
5. Clicks "Ready to Draft"
6. Waits for Ross to start auction

**Late Joiner (Jared):**
1. Joins during waiting room
2. Assigned to "Team Jared"
3. Tests bidding quickly
4. Marks ready when comfortable
5. Enters live auction with others

**Proxy Management (Netten no-show):**
1. Ross assigns himself as proxy for "Team Netten"
2. Tests bidding as proxy
3. Marks proxy team as ready
4. Manages multiple teams during auction

**Early Departure (Tilo):**
1. Tilo (on call): "I need to leave after this school"
2. Ross clicks [Take Proxy for Tilo]
3. Seamless transition during auction
4. If Tilo returns: Reconnection flow activates

## Success Criteria

### Pre-Auction Validation
- ✅ All participants have tested bidding interface
- ✅ All teams marked as "Ready to Draft"
- ✅ Nomination order visible and understood
- ✅ Any budget adjustments applied
- ✅ Proxy assignments confirmed

### Auction Master Confidence
- 📊 Clear visibility into who's ready vs still testing
- 🎛️ Easy controls for role assignment and proxy management
- 🔄 Simple reset options for test data
- 🚀 Confident "Start Real Auction" decision point

This specification ensures comprehensive testing while maintaining the flexibility needed for real-world auction scenarios with varied participation patterns.