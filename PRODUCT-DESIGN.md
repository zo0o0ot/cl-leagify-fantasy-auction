# Leagify Fantasy Auction - Product Design Document

## Overview
A real-time auction draft web application for fantasy sports, specifically designed for the NFL Draft League. Built with Blazor WASM, SignalR, and Azure services to support multiple simultaneous users bidding on college schools in a live auction format.

## Technical Architecture

### Core Technologies
- **Frontend:** Blazor WebAssembly (WASM) for client-side UI and logic
- **UI Framework:** Microsoft Fluent UI Blazor components for professional business interface
- **Real-time Communication:** Azure SignalR Service for live bidding updates
- **Backend:** Azure Functions (within Static Web Apps) for API endpoints
- **Database:** Azure SQL Database for persistent data storage
- **Hosting:** Azure Static Web Apps for deployment and hosting

### Service Integration Flow
1. User accesses app hosted on Azure Static Web Apps
2. Browser connects to Azure SignalR Service for real-time updates
3. User actions trigger C# API calls (Azure Functions in Static Web App)
4. API validates requests, updates Azure SQL Database
5. API broadcasts updates via Azure SignalR Service to all connected users

### Single Environment Strategy
- **Development and testing occur on deployed Azure infrastructure**
- Multi-user testing requires live deployment (cannot simulate SignalR locally)
- Database designed for multi-tenancy from day 1
- All features tested post-deployment before iteration

## Authentication & State Management

### User Authentication Flow
1. **Join Process:** User enters join code + display name (case-insensitive)
2. **Identity Creation:** System creates/retrieves user identity for that auction
3. **Role Assignment:** Auction Master assigns roles to named users
4. **Reconnection:** User re-enters same join code + name to resume session
5. **Approval:** Auction Master approves reconnection requests
6. **State Sync:** Reconnected users get sync loading screen then current auction state

### User Roles & Permissions
| Action                       | Auction Master | Team Coach | Proxy Coach | Auction Viewer |
| ---------------------------- | :------------: | :--------: | :---------: | :------------: |
| Create / Configure Auction   |       ✅       |     ❌     |      ❌     |       ❌       |
| Upload Draft Template (CSV)  |       ✅       |     ❌     |      ❌     |       ❌       |
| Assign Roles to Users        |       ✅       |     ❌     |      ❌     |       ❌       |
| Start / Pause / End Auction  |       ✅       |     ❌     |      ❌     |       ❌       |
| Nominate a School            |       ❌       |     ✅     |      ✅     |       ❌       |
| Bid on a School              |       ❌       |     ✅     |      ✅     |       ❌       |
| View All Rosters & Budgets   |       ✅       |     ✅     |      ✅     |       ✅       |
| Download Final Results       |       ✅       |     ✅     |      ✅     |       ✅       |

### State Management Principles
- **Server as Source of Truth:** All auction data maintained server-side
- **Client State Sync:** Blazor WASM clients display server state and send user actions
- **Disconnection Recovery:** Clients always re-sync state from server on reconnection
- **Real-time Updates:** SignalR broadcasts state changes to all connected clients

## School Management System

### Core Architecture
**Persistent School Data:**
- SchoolId (unique identifier)
- School Name (e.g., "Ohio State")
- LogoURL (External SVG URL from CSV - primary source)
- LogoFileName (Internal fallback file if external URL fails)
- Stored once, referenced by multiple auctions

**Auction-Specific School Data:**
- Links to SchoolId for identity and logo
- ProjectedPoints, Conference, LeagifyPosition (auction-specific stats)
- All calculated fields (ProjectedPointsAboveReplacement, etc.)

### CSV Import Process
1. **Parse Upload:** Read CSV draft template
2. **Data Validation:** Check for duplicate schools, negative values, missing required columns
3. **School Matching:** Fuzzy match school names to existing SchoolId records
4. **Confirmation:** Prompt Auction Master to confirm fuzzy matches before proceeding
5. **Create Missing:** Create new School entities for unrecognized schools
6. **Link Data:** Create AuctionSchool records linking SchoolId to auction-specific stats

**Validation Strategy:**
- **Clear Error Messages:** Specific row/column identification for validation failures
- **Manual Fix Expected:** User updates CSV and re-uploads rather than auto-correction
- **Common Issues:** Duplicate school names, negative ProjectedPoints, missing LeagifyPosition values
- **Validation Preview:** Show all detected issues before requiring CSV fix

### Logo Management Strategy
**Priority Order:**
1. **External URL (Primary):** Load SVG from LogoURL provided in CSV import
2. **Individual Upload:** Admin can upload replacement SVG for specific schools
3. **Bulk ZIP Upload:** Last resort for replacing multiple logos at once

**Implementation:**
- LogoURL attempted first for all logo displays
- On load failure, fallback to internal LogoFileName if available
- Final fallback to placeholder.svg for missing/broken logos
- Admin interface allows testing external URLs and uploading replacements
- Internal files stored in wwwroot/images/schools/ as static assets

## Auction Flow Design

### Pre-Auction Setup
1. **Auction Creation:** Auction Master creates new auction, gets unique join code
2. **CSV Upload:** Upload draft template with school data and statistics
3. **School Processing:** System matches/creates schools and imports auction data
4. **Roster Design:** Configure team roster structure (positions and quantities)
5. **Validation:** System validates enough schools exist for designed roster slots
6. **Team Setup:** Set team budgets and nomination order
7. **User Management:** Users join via code, Auction Master assigns roles

### Roster Configuration
- **Flexible Structure:** Auction Master defines position slots per team
- **Position Types:** Dynamically populated from CSV LeagifyPosition values
- **Flex Positions:** Special "any school" slots for strategic flexibility
- **Validation Logic:** Prevent rosters requiring more schools than available per position
- **Color Coding:** 8-color palette for position visualization

### Auction Process
1. **Nomination Order:** Pre-set by Auction Master, follows round-robin until roster full
2. **School Nomination:** Active Team Coach nominates school (auto-bids $1)
3. **Bidding Round:** Other coaches place bids or pass manually
4. **Bid Validation:** Server enforces budget constraints: `MaxBid = Budget - (EmptySlots - 1)`
5. **Auction End:** Manual "Pass" by all users OR Auction Master end button
6. **School Assignment:** Auto-assign to most restrictive valid position, user can override
7. **Next Nomination:** Advance to next coach with open roster slots

### Edge Case Handling
- **Disconnection During Win:** Auction Master can assign school to roster position
- **Full Rosters:** Skip users with no open slots in nomination order
- **Strategic Errors:** Allow users to make poor positioning decisions (part of strategy)
- **Budget Exhaustion:** Prevent nominations when user cannot afford minimum bid

## Real-time Communication Architecture

### SignalR Hub Design
**Connection Groups:**
- `auction-{auctionId}`: Regular participants get auction updates
- `admin-{auctionId}`: Auction Master gets additional admin-only broadcasts

**Core Hub Methods (Planned):**
- `PlaceBid(schoolId, amount)`
- `NominateSchool(schoolId)`
- `PassOnSchool()`
- `ApproveReconnection(userId)`
- `EndCurrentBid()`
- `AssignSchoolToPosition(teamId, schoolId, positionId)`

**Broadcast Events:**
- New bid placed (amount, bidder, current high)
- School won (winner, final price)
- User reconnection status
- Nomination turn changes
- Roster updates

### Admin Panel Features
**Auction Master Admin Interface:**
- **Connection Status:** Live view of connected/disconnected users
- **Role Management:** Proxy coach assignments and team mapping
- **Auction Control:** End current bid, assign schools to rosters
- **Reconnection Queue:** Approve pending reconnection requests
- **Audit Information:** Current budgets, roster states, bid history

## Data Calculation Logic

### Replacement Value Calculations
**ReplacementValueAverageForPosition:**
- Determine number of "starters" per position (configurable by Auction Master)
- Find the Nth+1 highest ProjectedPoints school for that position
- Example: 6 teams × 1 ACC slot = 6 starters, replacement = 7th best ACC school

**ProjectedPointsAboveReplacement:**
- `School.ProjectedPoints - Position.ReplacementValueAverageForPosition`
- Recalculated when team count or roster structure changes

### Recalculation Triggers
- **Manual Button:** Auction Master triggers recalculation
- **Team Count Changes:** When teams join/leave before auction starts
- **Roster Changes:** When position quantities are modified

## Administrative Features

### System Admin Interface
**Access Control:**
- Protected by master password/key in configuration
- Separate route `/admin` not linked from main UI
- Session-based authentication for convenience

**Cleanup Features:**
```
Auction Management Dashboard:
┌─────────────┬────────────┬─────────────┬──────────────┬─────────────┐
│ Join Code   │ Status     │ Last Active │ Participants │ Actions     │
├─────────────┼────────────┼─────────────┼──────────────┼─────────────┤
│ ABC123      │ Complete   │ 2 days ago  │ 6 users      │ [Archive]   │
│ XYZ789      │ In Progress│ 5 min ago   │ 4 users      │ [View][End] │
│ TEST001     │ Draft      │ 1 hour ago  │ 1 user       │ [Delete]    │
└─────────────┴────────────┴─────────────┴──────────────┴─────────────┘
```

**Safety Features:**
- Confirmation dialogs with auction code verification
- Export data before deletion options
- Audit trail of admin actions
- Bulk cleanup actions for testing data

**Test Data Management:**
- **Naming Convention:** Test auctions use "TEST-" prefix (e.g., "TEST-ABC123")
- **Production Testing:** Single environment used for both development and real auctions
- **Admin Cleanup Tools:** Bulk delete all TEST- prefixed auctions after development cycles
- **Data Isolation:** Test auctions clearly marked in admin interface for easy identification

### Logo Management Admin
- Add/edit school logos and basic information
- Bulk import school data from master lists
- SVG logo URL validation and testing

## User Experience Design

### Target Platform
- **Primary:** Desktop web browsers during live video calls
- **Secondary:** Mobile support as future enhancement
- **Context:** Users participate via video call, auction display is supplementary

### Interface Principles
- **Fluent Design Standards:** Follow Microsoft Fluent UI design patterns for professional appearance
- **Real-time Updates:** Immediate visual feedback for all auction events using FluentDataGrid
- **Clear Status:** Always show whose turn, current bid, remaining budget with FluentBadge and FluentCard
- **Error Prevention:** Disable invalid actions, show constraints clearly with FluentMessageBar
- **Reconnection UX:** Smooth re-entry flow with loading states using FluentProgressRing

### Auction Master Recovery
- **Master Recovery Code:** Separate from join code, allows Auction Master reconnection to any in-progress auction
- **Recovery Process:** Master enters recovery code + auction join code to resume control
- **State Restoration:** Reload current auction state from database (current bidder, high bid, roster states)
- **Admin Override:** Master can correct any inconsistencies found after reconnection

### Position Color Coding
- 8-color sensible palette for position differentiation
- Consistent colors across all users (not customizable per user)
- High contrast for accessibility
- Color-blind friendly palette selection

## Data Export & Results

### Final Results Export
**CSV Output Format (matching SampleFantasyDraft.csv):**
- Owner (team coach name)
- Player (school name)
- Position (assigned roster position)
- Bid (final auction price)
- ProjectedPoints (from original template)

**Export Availability:**
- All participants can download results after auction completion
- Real-time export during auction for Auction Master
- Historical exports via admin interface

## Deployment Architecture

### Azure Static Web Apps Configuration
- **Frontend:** Blazor WASM application
- **API:** Azure Functions for backend logic
- **Database:** Connection to Azure SQL Database
- **SignalR:** Integration with Azure SignalR Service

### Configuration Management
- Single environment serves both development and production
- Connection strings configured for production-grade services
- Feature flags for incomplete functionality during development

### Development Workflow
1. **Local Development:** Basic UI and business logic
2. **Deploy Early:** Any real-time or multi-user features require deployment
3. **Test Live:** Multi-user scenarios tested on deployed environment
4. **Iterate Fast:** Small, frequent deployments for rapid testing cycles

## Security Considerations

### Data Protection
- No sensitive personal information collected
- Join codes provide temporary access, not permanent accounts
- Audit trails for admin actions and bid history
- SQL injection prevention through parameterized queries

### Access Control
- Role-based permissions enforced server-side
- SignalR connection groups prevent cross-auction data leakage
- Admin interface protected by secure authentication
- Auction isolation prevents data cross-contamination

### Real-time Security
- All bid validations performed server-side
- Client-side UI updates are informational only
- State manipulation attempts rejected by server validation
- Connection authentication tied to auction membership

This design provides a comprehensive foundation for building a robust, scalable auction system that supports the specific needs of the NFL Draft League while maintaining flexibility for future enhancements.