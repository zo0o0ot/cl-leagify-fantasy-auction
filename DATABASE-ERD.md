# Leagify Fantasy Auction - Entity Relationship Diagram

## Core Entities and Relationships

### Schools (Master Data)
```
School
├── SchoolId (PK, int, identity)
├── Name (nvarchar(100), unique)
├── LogoURL (nvarchar(500), nullable) // External SVG URL (primary source)
├── LogoFileName (nvarchar(100), nullable) // Fallback internal file name
├── CreatedDate (datetime2)
└── ModifiedDate (datetime2)
```

### Auctions (Main Container)
```
Auction
├── AuctionId (PK, int, identity)
├── JoinCode (nvarchar(10), unique)
├── MasterRecoveryCode (nvarchar(20), unique) // For Auction Master reconnection
├── Name (nvarchar(100))
├── Status (nvarchar(20)) // Draft, InProgress, Complete, Archived
├── CreatedByUserId (int, FK to User)
├── CreatedDate (datetime2)
├── StartedDate (datetime2, nullable)
├── CompletedDate (datetime2, nullable)
├── CurrentNominatorUserId (int, FK to User, nullable)
├── CurrentSchoolId (int, FK to AuctionSchool, nullable)
├── CurrentHighBid (decimal(10,2), nullable)
├── CurrentHighBidderUserId (int, FK to User, nullable)
└── ModifiedDate (datetime2)
```

### Users (Auction Participants)
```
User
├── UserId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── DisplayName (nvarchar(50))
├── ConnectionId (nvarchar(100), nullable) // SignalR connection
├── IsConnected (bit)
├── JoinedDate (datetime2)
├── LastActiveDate (datetime2)
└── IsReconnectionPending (bit)

// Composite unique constraint on (AuctionId, DisplayName)
```

### Teams (Coach Assignments)
```
Team
├── TeamId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── UserId (int, FK to User) // Team Coach
├── TeamName (nvarchar(50), nullable)
├── Budget (decimal(10,2))
├── RemainingBudget (decimal(10,2))
├── NominationOrder (int)
└── IsActive (bit) // Can still nominate
```

### User Roles (Many-to-Many)
```
UserRole
├── UserRoleId (PK, int, identity)
├── UserId (int, FK to User)
├── TeamId (int, FK to Team, nullable) // For coaches/proxies
├── Role (nvarchar(20)) // AuctionMaster, TeamCoach, ProxyCoach, Viewer
└── AssignedDate (datetime2)
```

### Roster Design (Position Configuration)
```
RosterPosition
├── RosterPositionId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── PositionName (nvarchar(50)) // "Big Ten", "SEC", "Flex", etc.
├── SlotsPerTeam (int) // How many of this position each team has
├── ColorCode (nvarchar(7)) // Hex color for UI (#FF5733)
├── DisplayOrder (int)
└── IsFlexPosition (bit) // True for "any school" positions
```

### Auction Schools (School Data per Auction)
```
AuctionSchool
├── AuctionSchoolId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── SchoolId (int, FK to School)
├── Conference (nvarchar(50))
├── LeagifyPosition (nvarchar(50))
├── ProjectedPoints (decimal(8,2))
├── NumberOfProspects (int)
├── SuggestedAuctionValue (decimal(10,2), nullable)
├── ProjectedPointsAboveAverage (decimal(8,2))
├── ProjectedPointsAboveReplacement (decimal(8,2))
├── AveragePointsForPosition (decimal(8,2))
├── ReplacementValueAverageForPosition (decimal(8,2))
├── IsAvailable (bit) // Not yet drafted
└── ImportOrder (int) // Order from CSV for reference

// Composite unique constraint on (AuctionId, SchoolId)
```

### Draft Results (Final Picks)
```
DraftPick
├── DraftPickId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── TeamId (int, FK to Team)
├── AuctionSchoolId (int, FK to AuctionSchool)
├── RosterPositionId (int, FK to RosterPosition) // Where assigned
├── WinningBid (decimal(10,2))
├── NominatedByUserId (int, FK to User)
├── WonByUserId (int, FK to User) // Usually same as Team.UserId
├── PickOrder (int) // Order of selection in auction
├── DraftedDate (datetime2)
└── IsAssignmentConfirmed (bit) // User confirmed position assignment
```

### Bid History (Audit Trail)
```
BidHistory
├── BidHistoryId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── AuctionSchoolId (int, FK to AuctionSchool)
├── UserId (int, FK to User)
├── BidAmount (decimal(10,2))
├── BidType (nvarchar(20)) // Nomination, Bid, Pass
├── BidDate (datetime2)
├── IsWinningBid (bit)
└── Notes (nvarchar(200), nullable)
```

### Nomination Queue (Turn Order Management)
```
NominationOrder
├── NominationOrderId (PK, int, identity)
├── AuctionId (int, FK to Auction)
├── UserId (int, FK to User)
├── OrderPosition (int)
├── HasNominated (bit) // For current round
└── IsSkipped (bit) // Roster full, skip in rotation
```

### Admin Audit Log
```
AdminAction
├── AdminActionId (PK, int, identity)
├── AuctionId (int, FK to Auction, nullable)
├── AdminUserId (int, FK to User, nullable)
├── ActionType (nvarchar(50)) // Delete, Archive, ForceEnd, etc.
├── Description (nvarchar(500))
├── ActionDate (datetime2)
└── IPAddress (nvarchar(45), nullable)
```

## Key Relationships

### One-to-Many Relationships
- **Auction** → **Users** (1:N) - One auction has many participants
- **Auction** → **Teams** (1:N) - One auction has many teams
- **Auction** → **AuctionSchools** (1:N) - One auction has many schools
- **Auction** → **RosterPositions** (1:N) - One auction has many position types
- **Team** → **DraftPicks** (1:N) - One team makes many picks
- **School** → **AuctionSchools** (1:N) - One school appears in many auctions
- **User** → **BidHistory** (1:N) - One user makes many bids

### Many-to-Many Relationships
- **Users** ↔ **Teams** (via UserRole) - Proxy coaches can represent multiple teams
- **Users** ↔ **Roles** (via UserRole) - Users can have multiple roles

### Foreign Key Constraints
- All FK relationships enforce referential integrity
- Cascade delete where appropriate (Auction deletion removes all child records)
- Restrict delete for core entities (cannot delete School if referenced)

## Indexes for Performance

### Primary Indexes (Automatic)
- All primary keys have clustered indexes

### Secondary Indexes (Recommended)
```sql
-- Auction lookup by join code
CREATE INDEX IX_Auction_JoinCode ON Auction(JoinCode)

-- User lookup within auction
CREATE INDEX IX_User_AuctionId_DisplayName ON User(AuctionId, DisplayName)

-- Active schools in auction
CREATE INDEX IX_AuctionSchool_AuctionId_Available ON AuctionSchool(AuctionId, IsAvailable)

-- Team roster lookup
CREATE INDEX IX_DraftPick_TeamId ON DraftPick(TeamId)

-- Bid history for school
CREATE INDEX IX_BidHistory_AuctionSchoolId_BidDate ON BidHistory(AuctionSchoolId, BidDate)

-- Connection management
CREATE INDEX IX_User_ConnectionId ON User(ConnectionId) WHERE ConnectionId IS NOT NULL
```

## Data Integrity Constraints

### Business Rules Enforced in Database
```sql
-- Budget constraints
ALTER TABLE Team ADD CONSTRAINT CK_Team_Budget_Positive 
CHECK (Budget > 0 AND RemainingBudget >= 0 AND RemainingBudget <= Budget)

-- Bid amounts must be positive
ALTER TABLE BidHistory ADD CONSTRAINT CK_BidHistory_Amount_Positive 
CHECK (BidAmount > 0)

-- Winning bid must match final pick
ALTER TABLE DraftPick ADD CONSTRAINT CK_DraftPick_WinningBid_Positive 
CHECK (WinningBid > 0)

-- Position slots must be positive
ALTER TABLE RosterPosition ADD CONSTRAINT CK_RosterPosition_Slots_Positive 
CHECK (SlotsPerTeam > 0)

-- Hex color format validation
ALTER TABLE RosterPosition ADD CONSTRAINT CK_RosterPosition_ColorCode_Format 
CHECK (ColorCode LIKE '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]')
```

### Unique Constraints
```sql
-- One school per auction
ALTER TABLE AuctionSchool ADD CONSTRAINT UQ_AuctionSchool_AuctionId_SchoolId 
UNIQUE (AuctionId, SchoolId)

-- Unique display names per auction
ALTER TABLE User ADD CONSTRAINT UQ_User_AuctionId_DisplayName 
UNIQUE (AuctionId, DisplayName)

-- Unique join codes
ALTER TABLE Auction ADD CONSTRAINT UQ_Auction_JoinCode 
UNIQUE (JoinCode)

-- Unique master recovery codes
ALTER TABLE Auction ADD CONSTRAINT UQ_Auction_MasterRecoveryCode 
UNIQUE (MasterRecoveryCode)

-- One school assignment per roster position per team
ALTER TABLE DraftPick ADD CONSTRAINT UQ_DraftPick_Team_Position_School 
UNIQUE (TeamId, RosterPositionId, AuctionSchoolId)
```

## Multi-Tenancy Pattern

### Auction Isolation
- All queries filtered by AuctionId to prevent cross-auction data leakage
- Row-level security policies can be implemented for additional protection
- Connection strings and database access scoped appropriately

### Data Partitioning Considerations
```sql
-- Partition large tables by AuctionId if needed in future
-- CREATE PARTITION FUNCTION pf_AuctionId (int)
-- Beneficial for BidHistory and AdminAction tables with high volume
```

## Sample Data Relationships

### Typical Auction Structure
```
Auction "ABC123"
├── Users: 7 (1 Auction Master + 6 Team Coaches)
├── Teams: 6 (one per coach)
├── RosterPositions: 8 (Big Ten×2, SEC×2, ACC×1, Big12×1, SmallSchool×1, Flex×3)
├── AuctionSchools: 144 (from CSV import)
├── DraftPicks: 48 (6 teams × 8 roster slots)
└── BidHistory: ~200-500 (depending on auction activity)
```

## Migration Strategy

### Phase 1: Core Schema
1. Create School, Auction, User, Team tables
2. Basic relationships and constraints
3. Admin authentication and cleanup functionality

### Phase 2: Auction Logic
1. Add RosterPosition, AuctionSchool tables
2. Import and matching logic for CSV data
3. Auction setup and configuration

### Phase 3: Live Bidding
1. Add DraftPick, BidHistory, NominationOrder tables
2. Real-time state management
3. SignalR integration points

### Phase 4: Audit and Admin
1. Complete AdminAction logging
2. Performance indexes
3. Advanced cleanup and management features

This ERD provides a solid foundation for the multi-tenant auction system while maintaining data integrity and supporting all the business requirements discussed.