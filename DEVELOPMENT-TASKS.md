# Leagify Fantasy Auction - Development Task Breakdown

## Implementation Priority Order

Based on the requirement: **School Management → Auction Creation → Join Auction → Real-time Bidding**

---

## Phase 1: Foundation & School Management (Weeks 1-2)

### Task 1.1: Project Setup and Azure Infrastructure
**Priority:** Critical  
**Estimated Effort:** 4-6 hours  
**Dependencies:** None

- [x] Install Microsoft Fluent UI Blazor templates: `dotnet new install Microsoft.FluentUI.AspNetCore.Templates`
- [x] Create Blazor WebAssembly project with Fluent UI template
- [x] Set up Azure SQL Database with connection strings
- [x] Configure Azure SignalR Service (basic setup)
- [x] Implement basic project structure (Models, Services, Pages) following C# 13/.NET 8+ patterns
- [x] Add Microsoft.Extensions.Logging for structured logging
- [x] Create deployment workflow for Azure Static Web Apps
- [x] Verify deployed app loads correctly with Fluent UI components

**Deliverables:**
- Working Blazor app deployed to Azure
- Database connectivity established
- Basic CI/CD pipeline functional

### Task 1.2: School Entity Management System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 1.1

- [x] Create School entity and database schema
- [x] Implement School repository with CRUD operations (in-memory for now)
- [x] Build management interface using FluentDataGrid for school management
- [x] Add external logo URL loading with fallback to internal files using FluentTextField
- [x] Create school search and fuzzy matching logic
- [x] Implement school creation from unmatched names
- [x] Restore FluentUI framework with proper MIME configuration
- [x] Convert basic HTML table to FluentDataGrid with built-in pagination (20 items per page)
- [x] Add sortable columns and professional FluentUI styling
- [x] Implement CSV import with logo download functionality
- [ ] Add comprehensive XML documentation for all public school management APIs
- [ ] Implement unit tests using xUnit with AAA pattern for school services

**Deliverables:**
- [x] School database table with sample data (130 schools from template)
- [x] Admin interface for managing schools with FluentUI components
- [x] Fuzzy matching algorithm for school names
- [x] FluentDataGrid with pagination, sorting, and professional styling
- [x] CSV import system with logo download capabilities
- [x] Complete CRUD operations for school management

### Task 1.3: System Management Authentication
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 1.1

- [x] Implement management authentication with master password
- [x] Create `/api/management/*` API routes (due to Azure Static Web Apps `/admin` restrictions)
- [x] Add token-based authentication for management operations
- [x] Create management interface and basic dashboard
- [x] Implement logout and token cleanup
- [x] Add comprehensive authentication middleware to school management endpoints
- [x] Implement secure login page with FluentUI components
- [x] Add automatic token validation and redirect logic
- [ ] Note: Separate from auction join code system

**Deliverables:**
- [x] Secure management API with password protection
- [x] Token-based authentication system with 8-hour expiry
- [x] Professional FluentUI login interface
- [x] Automatic authentication state management and redirects
- [x] Protected management dashboard structure

---

## Phase 2: Auction Creation & Configuration (Weeks 2-3)

### Task 2.1: Core Auction Entities
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 1.2

- [ ] Create Auction, User, Team database schema
- [ ] Implement basic Auction entity with join code generation
- [ ] Add Auction status management (Draft, InProgress, Complete)
- [ ] Create auction repository with basic CRUD operations
- [ ] Implement join code validation and uniqueness

**Deliverables:**
- Auction database schema implemented
- Join code generation system
- Basic auction creation functionality

### Task 2.2: CSV Import System
**Priority:** Critical  
**Estimated Effort:** 10-12 hours  
**Dependencies:** Task 2.1

- [ ] Create CSV parsing service for draft templates with comprehensive XML documentation
- [ ] Implement AuctionSchool entity and mapping using primary constructor syntax
- [ ] Build school matching interface using FluentDialog for confirmation prompts
- [ ] Add validation for required CSV columns with FluentValidation or DataAnnotations
- [ ] Create import error handling with structured logging and user feedback
- [ ] Implement data preview using FluentDataGrid before final import
- [ ] Add unit tests for CSV parsing and validation logic

**Deliverables:**
- CSV upload and parsing functionality
- School matching with Auction Master confirmation
- AuctionSchool data populated from imports

### Task 2.3: Roster Design Interface
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 2.2

- [ ] Create RosterPosition entity using primary constructor syntax with XML documentation
- [ ] Build roster design UI using FluentTextField, FluentSelect, and FluentButton components
- [ ] Implement position validation against available schools with structured logging
- [ ] Add color picker using FluentColorPicker for position coding
- [ ] Create roster preview using FluentDataGrid and team slot calculation
- [ ] Implement validation warnings with FluentMessageBar for impossible rosters
- [ ] Add unit tests for roster validation business logic

**Deliverables:**
- Roster design interface for Auction Masters
- Position validation system
- Color coding for positions

### Task 2.4: Auction Setup Completion
**Priority:** High  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 2.3

- [ ] Create auction configuration summary page
- [ ] Implement budget setting per team
- [ ] Add nomination order configuration
- [ ] Create auction validation before start
- [ ] Build auction preview for Auction Master review
- [ ] Add recalculation logic for replacement values

**Deliverables:**
- Complete auction setup workflow
- Budget and order configuration
- Validation system preventing invalid auctions

---

## Phase 3: User Join & Role Management (Week 4)

### Task 3.1: Join Auction Flow
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 2.1

- [ ] Create join auction page with code entry
- [ ] Implement user registration with display name
- [ ] Add case-insensitive name matching for returning users
- [ ] Create user session management
- [ ] Implement basic auction participant view
- [ ] Add join validation (auction exists, not full, etc.)

**Deliverables:**
- Join auction functionality
- User registration and session management
- Basic participant dashboard

### Task 3.2: Role Assignment System
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 3.1

- [ ] Create UserRole entity and management system
- [ ] Build Auction Master interface for role assignment
- [ ] Implement team assignment for coaches
- [ ] Add proxy coach assignment to multiple teams
- [ ] Create role-based permission enforcement
- [ ] Build role switching UI for proxy coaches

**Deliverables:**
- Role assignment interface for Auction Masters
- Permission system enforcing role capabilities
- Proxy coach team switching functionality

### Task 3.3: Reconnection System
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 3.2

- [ ] Implement reconnection approval workflow
- [ ] Create pending reconnection queue for Auction Master
- [ ] Add state synchronization on reconnection
- [ ] Build loading screen for state sync
- [ ] Implement connection status tracking
- [ ] Add reconnection notifications

**Deliverables:**
- Reconnection approval system
- State synchronization on user return
- Connection status management

---

## Phase 4: Basic SignalR Integration (Week 5)

### Task 4.1: SignalR Hub Foundation
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 3.3

- [ ] Create AuctionHub with basic connection management
- [ ] Implement auction-specific SignalR groups
- [ ] Add user authentication to SignalR connections
- [ ] Create connection/disconnection event handling
- [ ] Build basic real-time status updates
- [ ] Add error handling for SignalR operations

**Deliverables:**
- Working SignalR hub with auction groups
- Connection management system
- Basic real-time communication

### Task 4.2: Admin Panel Real-time Features
**Priority:** High  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 4.1

- [ ] Create Auction Master management panel (separate tab)
- [ ] Implement real-time connection status display using FluentDataGrid
- [ ] Add proxy coach assignment visualization with FluentCard components
- [ ] Create reconnection approval interface using FluentDialog
- [ ] Build management-only SignalR broadcasts with structured logging
- [ ] Add auction control buttons using FluentButton (pause, end, etc.)
- [ ] Implement comprehensive unit tests for SignalR hub methods

**Deliverables:**
- Auction Master management panel with real-time updates
- Connection status monitoring
- Administrative controls for auction management

---

## Phase 5: Real-time Bidding System (Weeks 6-7)

### Task 5.1: Nomination System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 4.2

- [ ] Create NominationOrder entity and management
- [ ] Implement turn-based nomination logic
- [ ] Build school nomination interface
- [ ] Add nomination validation (roster space, budget)
- [ ] Create automatic turn advancement
- [ ] Implement nomination broadcasts via SignalR

**Deliverables:**
- Turn-based nomination system
- Real-time nomination updates
- Nomination validation and turn management

### Task 5.2: Bidding System
**Priority:** Critical  
**Estimated Effort:** 10-12 hours  
**Dependencies:** Task 5.1

- [ ] Create BidHistory entity and tracking
- [ ] Implement bid placement with validation
- [ ] Build bidding interface with current high bid display using FluentTextField and FluentButton
- [ ] Add budget constraint enforcement with FluentValidation and user feedback
- [ ] Create pass functionality using FluentButton for all users
- [ ] Implement Auction Master end-bidding controls with FluentDialog confirmations
- [ ] Add comprehensive unit tests for bidding validation logic

**Deliverables:**
- Complete bidding system with validation
- Real-time bid updates
- Budget enforcement and pass functionality

### Task 5.3: Draft Completion System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 5.2

- [ ] Create DraftPick entity for final results
- [ ] Implement school assignment to roster positions
- [ ] Build auto-assignment with manual override
- [ ] Add roster validation and completion tracking
- [ ] Create auction end detection logic
- [ ] Implement final results compilation

**Deliverables:**
- School assignment to roster positions
- Auto-assignment with manual override capability
- Auction completion detection and results

### Task 5.4: Results Export System
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 5.3

- [ ] Create CSV export functionality matching sample format
- [ ] Implement results download for all participants
- [ ] Add real-time results view during auction
- [ ] Create historical results access
- [ ] Build results formatting and validation

**Deliverables:**
- CSV export matching required format
- Results download functionality
- Real-time results display

---

## Phase 6: Admin Management & Cleanup (Week 8)

### Task 6.1: Auction Management Interface
**Priority:** Medium  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 4.2

- [ ] Build comprehensive auction list for management interface
- [ ] Add auction status filtering and search
- [ ] Implement auction deletion with confirmations
- [ ] Create bulk cleanup operations
- [ ] Add auction archiving functionality
- [ ] Build auction details view for management interface

**Deliverables:**
- Complete management auction interface
- Bulk operations for cleanup
- Safe deletion with confirmations

### Task 6.2: School Logo Management
**Priority:** Low  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 1.2

- [ ] Implement logo loading strategy: External URL → Internal file → Placeholder fallback
- [ ] Add logo URL testing and validation with preview
- [ ] Create individual logo upload interface for broken external URLs
- [ ] Implement bulk ZIP logo upload as last resort option
- [ ] Add school statistics and logo availability tracking

**Deliverables:**
- Enhanced school management with logo features
- Bulk import capabilities
- School usage analytics

### Task 6.3: Audit and Monitoring
**Priority:** Medium  
**Estimated Effort:** 4-6 hours  
**Dependencies:** All previous tasks

- [ ] Create AdminAction logging system
- [ ] Implement comprehensive audit trails
- [ ] Add performance monitoring for SignalR
- [ ] Create error logging and alerting
- [ ] Build system health dashboard
- [ ] Add usage analytics and reporting

**Deliverables:**
- Complete audit logging system
- Performance monitoring
- System health and usage analytics

---

## Testing Strategy Per Phase

### Development Testing Approach
- **Local Development:** Basic UI and business logic testing
- **Deployment Testing:** All real-time and multi-user features
- **Multi-User Scenarios:** Test with multiple browser windows/devices
- **Edge Case Testing:** Network disconnections, invalid data, concurrent operations

### Phase-Specific Testing
1. **Phase 1-2:** School management, CSV imports, auction setup
2. **Phase 3:** User joining, role assignments, permissions
3. **Phase 4:** SignalR connections, real-time updates
4. **Phase 5:** Complete auction flow with multiple users
5. **Phase 6:** Admin operations, cleanup, edge cases

---

## Risk Mitigation

### High-Risk Areas
- **SignalR Integration:** Complex real-time state management
- **Multi-User Bidding:** Concurrent access and race conditions
- **State Synchronization:** Maintaining consistency across clients
- **Budget Validation:** Complex business logic enforcement

### Mitigation Strategies
- **Early SignalR Testing:** Implement basic real-time features early
- **Server-Side Validation:** All critical business logic on server
- **Incremental Complexity:** Start with simple features, add complexity
- **Frequent Deployment:** Test each feature in production environment

---

## Success Criteria Per Phase

### Phase 1 Success
- [x] Deployed app accessible via Azure
- [x] Schools can be added and managed with full CRUD operations
- [x] FluentUI framework properly integrated with pagination and sorting
- [x] CSV import system functional with logo downloads
- [x] Admin authentication working with secure token-based system

### Phase 2 Success
- [ ] CSV upload and school matching functional
- [ ] Roster design with position validation
- [ ] Complete auction setup workflow

### Phase 3 Success
- [ ] Users can join auctions via join code
- [ ] Role assignment by Auction Master
- [ ] Reconnection system working

### Phase 4 Success
- [ ] Real-time connection status
- [ ] Admin panel with live updates
- [ ] SignalR communication established

### Phase 5 Success
- [ ] Complete auction with multiple users
- [ ] Bidding system with all validations
- [ ] Results export in correct format

### Phase 6 Success
- [ ] Admin cleanup operations
- [ ] Audit logging functional
- [ ] Production-ready system

This task breakdown provides achievable milestones while building toward the complete auction system. Each task includes specific deliverables and can be tested independently on the deployed Azure environment.